# Integrations — the events feed (v1)

Other programs on the streamer's PC (Streamer.bot, a Stream Deck plugin, custom alerts, a
script) can follow what Pity Sense sees. The feed is **read-only** and **local only**: it
never leaves the PC, and nothing a program sends to it changes Pity Sense.

- **Subscribe:** `ws://127.0.0.1:47823/events` — a WebSocket; every message is one JSON
  object. On connect you get `hello`, then the current `state` (if there is one), then
  events as they happen.
- **Poll:** `GET http://127.0.0.1:47823/api/state` — the current `state` object
  (`{"type":"state","ready":false}` until Pity Sense has one).
- If port 47823 is taken, Pity Sense uses the next free one (47824 …); `GET /api/info` on
  the port you find answers `{"app":"Pity Sense", ...}`.

**Version promise.** Every event carries `"v": 1`. Fields are only ever *added*; nothing
in v1 is renamed, removed or changes type. A new major version would come with a new `v`
and the old one kept for a long overlap.

Every event has `v`, `type` and `at` (ISO 8601 time, local clock). Every event that has
`game` also has `gameName` (e.g. `"Honkai: Star Rail"`), and every event that has `banner`
also has `bannerName` (e.g. `"Character Warp"`) — the names an alert would show.

## Events

### `hello`
First message on connect. `app` ("Pity Sense"), `version`.

### `state`
Sent whenever anything in it changes (never more than once per change), and to every new
subscriber.

| field | meaning |
|---|---|
| `game` | `"hsr"` or `"wuwa"` |
| `gameName` | e.g. `"Honkai: Star Rail"` |
| `banner` | banner key, e.g. `"character"`, `"lightcone"`, `"standard"`, `"weapon"` |
| `bannerName` | e.g. `"Character Warp"` |
| `pity` | pulls since the last 5★ on this banner |
| `hardPity`, `softPity` | this banner's hard pity and where soft pity starts |
| `guaranteed` | `true` if the next 5★ is guaranteed featured |
| `pityUncertain` | how many pulls the pity could be off by (0 = exact) |
| `featuredName` | the featured 5★ as read off the banner, or `null` |
| `pullsAvailable` | pulls the streamer can afford, read off the game's counters, or `null` |
| `session` | `{ active, pulls, fourStars, fiveStars, won5050, lost5050 }` |
| `lifetime` | `{ pulls, fiveStars, won5050, lost5050 }` |
| `lastFiveStar` | `{ name, pity, outcome, at }` or `null` |
| `paused` | `true` while "pulling on someone else's account" is ticked |

### `session_start`
`game`, `banner`.

### `pull`
A pull (single or 10) was counted. `pulls` (1 or 10), `fourStars`, `fiveStars`, `exact`
(every card read; `false` when some were unreadable), `source` (`"detector"` or
`"manual"`), `pityBefore`, `pity` (after), `game`, `banner`.

### `five_star`
One per 5★ in a pull. `name` (or `null` if it couldn't be read), `pity` (the pity it
landed at, or `null` if unknown), `outcome`: `"pending"` (50/50 not decided yet),
`"won"`, `"lost"`, `"guaranteed"`, `"featured"` (banners with no 50/50), `"standard"`.
Plus `game`, `banner`.

### `fifty_fifty`
A 50/50 was decided: `won` (bool), `name`, `byName` (`true` when Pity Sense settled it
from the name it read, `false` when the streamer answered). Plus `game`, `banner`.

### `session_end`
`pulls`, `fourStars`, `fiveStars`, `won5050`, `lost5050`, `averagePity` (or `null`),
`game`, and `banners`: one entry per banner pulled on
(`{ banner, bannerName, pulls, fourStars, fiveStars, won5050, lost5050 }`).

## Examples

### Streamer.bot (ready-made)
One action, pasted once — no WebSocket client to set up, no JSON to handle:

1. In Pity Sense: **Settings → Streamer.bot & other tools → Copy the Streamer.bot code**
   (or take [`streamerbot/PitySense.cs`](streamerbot/PitySense.cs) from this page).
2. In Streamer.bot: **Actions** → right-click → **Add**, name it `Pity Sense`.
3. In that action: **Sub-Actions** → right-click → **Core → C# → Execute C# Code**. Paste
   the code over what's there, tick **Precompile on application start**, then
   **Save and Compile**.
4. On any of your actions: **Triggers** → right-click → **Custom → Pity Sense**.

| trigger | arguments for your sub-actions |
|---|---|
| 5★ pulled | `%name%` `%pity%` `%outcome%` `%bannerName%` `%gameName%` |
| 50/50 won · 50/50 lost | `%name%` `%bannerName%` `%gameName%` `%byName%` |
| Pull counted | `%pulls%` `%fourStars%` `%fiveStars%` `%pityBefore%` `%pity%` `%bannerName%` |
| Soft pity reached | `%pity%` `%softPity%` `%hardPity%` `%guaranteed%` `%bannerName%` |
| Pity changed | `%pity%` `%hardPity%` `%softPity%` `%guaranteed%` `%pullsAvailable%` `%bannerName%` |
| Pull session started · ended | ended: `%pulls%` `%fourStars%` `%fiveStars%` `%won5050%` `%lost5050%` `%averagePity%` |
| Connected · Disconnected | `%version%` |

Every trigger also has `%pitySenseEvent%` and `%pitySenseJson%` (the event as sent). Global
variables for text sources and chat messages: `~pitySensePity~`, `~pitySenseHardPity~`,
`~pitySenseGuaranteed~`, `~pitySenseBanner~`, `~pitySensePullsAvailable~`,
`~pitySenseLastFiveStar~` and more (the list is at the top of the code). The action finds
Pity Sense's port by itself, reconnects when Pity Sense restarts, and only listens.

**Node.js**
```js
const ws = new WebSocket("ws://127.0.0.1:47823/events");
ws.onmessage = (m) => {
  const e = JSON.parse(m.data);
  if (e.type === "five_star") console.log(`5★ ${e.name ?? "?"} at pity ${e.pity}`);
  if (e.type === "state") console.log(`${e.bannerName}: ${e.pity}/${e.hardPity}`);
};
```

**Python**
```python
import json, websockets, asyncio   # pip install websockets
async def main():
    async with websockets.connect("ws://127.0.0.1:47823/events") as ws:
        async for raw in ws:
            e = json.loads(raw)
            if e["type"] == "pull": print(e["pulls"], "pulls, pity now", e["pity"])
asyncio.run(main())
```

**Stream Deck / anything that polls:** `curl http://127.0.0.1:47823/api/state`.

## What the feed is not
It is not a way to change Pity Sense (set pity, count a pull): those stay with the
streamer in the panel. Messages sent to `/events` are ignored. A website open in a
browser can't subscribe (its origin is refused); programs on the PC can.
