# Vendored font policy

The foundation bundles only open fonts needed for consistent offline rendering:

- Inter
- IBM Plex Sans
- IBM Plex Mono
- Source Serif 4

They are served under `HisHope Inter`, `HisHope Plex Sans`, `HisHope Plex Mono` and `HisHope Source Serif` to avoid colliding with an application's own font packages. The files are distributed under their respective upstream open licenses; see the upstream projects for license text and notices:

- [Inter](https://github.com/rsms/inter)
- [IBM Plex](https://github.com/IBM/plex)
- [Source Serif](https://github.com/adobe-fonts/source-serif)

The catalog includes proprietary families such as SF Pro, Linear, Saans and SpotifyMix. Those are not redistributed. Presets use the bundled open fallback family so Vietnamese and other supported UI text remains available without a network request.
