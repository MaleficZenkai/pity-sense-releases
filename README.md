# Pity Sense — user guide

Pity Sense watches your game window while you pull and keeps your pity, 50/50 and session
numbers on a small card in your stream. Wuthering Waves and Honkai: Star Rail, on Windows.

Download the latest `PitySense-Setup-x.y.z.exe` from
[Releases](https://github.com/MaleficZenkai/pity-sense-releases/releases/latest). Once
installed, it updates itself.

## Getting started (about two minutes)
1. **Install and open Pity Sense.** Windows may warn that the publisher is unknown — the
   installer isn't signed yet. Choose *More info → Run anyway*.
2. **Add the overlay to OBS.** In Pity Sense, Settings → OBS: paste your OBS WebSocket
   password (OBS: *Tools → WebSocket Server Settings → Enable WebSocket server → Show
   Connect Info*) and click **Connect**. Pity Sense adds the overlay to your scene and keeps
   it up to date by itself. Using Streamlabs or something else? *Settings → Overlay → Add
   the overlay by hand* gives you the address to add as a browser source.
3. **Start your game.** Pity Sense finds the game window by itself.
4. **Open the Warp / Convene screen once**, any banner. That lets Pity Sense line itself up
   with your game.
5. **Set your current pity** on the Live tab (the number the game shows under your pulls).

The Live tab shows these as a checklist and ticks them off as you go.

## What the card shows
- **Pity** — pulls since your last 5★, out of the hard pity for that banner (90 for a
  Star Rail character warp, 80 for light cones and for Wuthering Waves).
- **50/50** — whether your next 5★ is guaranteed to be the featured one.
- **Pulls available** — read off the game's own currency counters on the banner screen.
- **Session** — pulls, 5★ and 4★ since you clicked *Start pull session*; a recap card can
  appear when the session ends.
- **5★ moments** — a pop-up when a 5★ lands, with the name when Pity Sense could read it.

## Pull sessions
Click **Start pull session** before you pull and **End pull session** after. Only pulls
inside a session are counted; anything that looks like a pull outside one is held with a
*Count it?* question, so a loading screen or a shop can never add to your numbers by
mistake. A session stays open until you end it, so a chat break mid-session never stops
the counting.

**Pulling on a viewer's account?** Tick *Pulling on someone else's account* on the Live tab;
Pity Sense ignores everything until you untick it.

## Fixing a number
*Live → Fix the numbers* lets you set the pity, the 50/50, add or remove pulls, and **Undo**
takes back the last change. Every change is kept in a log (the Pull log tab) so you can
always see where a number came from. You can export the whole history as a spreadsheet.

## Twitch extras (optional, all off by default)
Settings → Twitch extras → **Connect Twitch**. Then you can switch on:
- **Chat commands**: `!pity`, `!luck`, `!5050`, `!bless` (before a pull), `!lucky`.
- **Chat announcements** of 5★ and session recaps.
- **Auto Predictions** ("5★ in the next 10?") — needs Affiliate or Partner.
- **Auto clips** of 5★ moments (only while live).

## Integrations (Streamer.bot, Stream Deck, custom alerts)
Other programs on your PC can follow your pity, pulls and 5★ live through a small
read-only feed — see
[INTEGRATIONS.md](https://github.com/MaleficZenkai/pity-sense-releases/blob/main/INTEGRATIONS.md).
Nothing leaves your PC, and nothing a program sends can change your numbers.

## Updates
Pity Sense checks for updates when it starts and every six hours while running. When one is
ready, a bar at the top says *Restart now*. After an update, *What's new* shows what
changed. Settings → App → *Get test versions first (beta)* gives you new versions a few
days early.

New characters, weapons and banners usually arrive without an update at all: Pity Sense
fetches a small signed "game pack" about once an hour.

## Privacy — what stays on your PC, what doesn't
- **Your numbers, pull history and settings** are files in `%APPDATA%\Pity Sense`, with
  daily backups. Nothing about your pulls is sent anywhere.
- **Pull recordings** (Settings → Detection → *Save a video of each pull*) are off by
  default and stay on your PC.
- **Sent over the internet:** the update check and the game-pack fetch (GitHub), and
  Twitch calls if you connected Twitch. Problem reports are opt-in and never include
  passwords or logins.
- Your OBS and Twitch passwords are stored encrypted with Windows' per-user encryption.

## Is this safe to use with the games?
Pity Sense only *looks at* the game window, through Windows' screen capture — the same
thing OBS does to show your game on stream. It never reads the game's memory or files,
never sends it input, never changes anything about it, and doesn't attach to it in any way.
Nobody can promise what a game's rules will say tomorrow, so use your own judgement; if you
want certainty, ask the game's support whether capturing the game window is allowed (it's
what every streamer does).

## When something goes wrong
The status line at the top says what happened and what to do. If it keeps happening:
Settings → App → **Save diagnostics** makes a file with Pity Sense's version, settings and
recent logs (passwords and logins left out) that you can send along with your report.

Common ones:
- **"Windows didn't let Pity Sense capture the game"** — restart Pity Sense.
- **"The game can't be captured right now"** — if the game runs as administrator, run Pity
  Sense as administrator too (or restart the game).
- **"Can't reach OBS"** — start OBS and check *Tools → WebSocket Server Settings → Enable
  WebSocket server*.
- **"OBS didn't accept the password"** — copy it again from *Show Connect Info*.
- **Pity Sense counted something that wasn't a pull** — click *Undo*, then tell us: the
  pull recordings (Settings → Detection) are what make the next version better.

## Requirements
Windows 10 or 11 (64-bit), the game in a window or borderless window, OBS 28 or newer for
the automatic overlay setup. Any screen size and shape, including ultrawide.
