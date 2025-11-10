[![Contributors][contributors-shield]][contributors-url]
[![Forks][forks-shield]][forks-url]
[![Stargazers][stars-shield]][stars-url]
[![Issues][issues-shield]][issues-url]
[![MIT License][license-shield]][license-url]

<!-- PROJECT LOGO -->
<br />
<div align="center">
  <a href="https://github.com/JoeRu/ImmichFrame">
    <img src="design/AppIcon.png" alt="Logo" width="200" height="200">
  </a>

  <h3 align="center">ImmichFrame</h3>

  <p align="center">
    An awesome way to display your photos as a digital photo frame
    <br />
    <a href="https://immich.app/"><strong>Explore immich »</strong></a>
    <br />
    <br />
    <a href="https://immichframe.dev">Documentation</a>
    ·
    <a href="https://demo.immichframe.dev">Demo</a>
    ·
    <a href="https://github.com/JoeRu/ImmichFrame/issues">Report Bug</a>
    ·
    <a href="https://github.com/JoeRu/ImmichFrame/issues">Request Feature</a>
  </p>
</div>

### 🎬 Video Branch Features

> **Note**: This branch includes enhanced features not available in the main branch.

**Usage of this FORK:**

build a docker-compose.yml with following content:
```
name: immichframe
services:
  immichframe:
    container_name: immichframe
    image: jayar79/immichframe:test
#or (not yet implemented)
#jayar79/immichframe:prod
#or (not yet implemented)
#jayar79/immichframe:dev
#this is the original
#ghcr.io/immichframe/immichframe:latest
    restart: on-failure
    volumes:
      - ./Config:/app/Config
      - ./Config/custom.css:/app/wwwroot/static/custom.css
      - ./Config/test.txt:/app/wwwroot/static/test.txt
    ports:
      - "2284:8080"
```

#### 🆕 New Features in Video Branch:

- **📹 Video Asset Support**: 
  - Display video files from your Immich library in the slideshow
  - Seamless playback of MP4, MOV, and other supported video formats
  - Video controls and automatic progression in slideshow mode

- **🎨 Dynamic Color Adaptation**: 
  - Real-time color extraction from currently displayed assets (images and videos)
  - Automatic UI theming with complementary colors derived from asset content
  - Enhanced text contrast with WCAG AAA compliance (7:1 ratio) for optimal readability
  - Shadow-free text rendering with enhanced typography for better legibility

- **📅 Chronological Asset Sets** (RandomDateAssetsPool Enhancement):
  - Improved balanced photo selection across your entire library timeline
  - Cluster-based algorithm prevents bias toward newer photos
  - Better temporal distribution for truly random chronological selection

#### 🔧 Technical Improvements:
- **Frontend**: Enhanced Svelte components with Canvas API color extraction
- **Backend**: Optimized C# asset pool algorithms with bias and Error of shuffling removal
- **UI/UX**: Improved contrast handling and typography without blur effects

### ⚙️ Configuration Guide

#### Chronological Asset Selection

The **`ChronologicalImagesCount`** setting controls how many assets are selected using the enhanced chronological algorithm:

```json
{
  "ChronologicalImagesCount": 0
}
```

**Values:**
- **`0` (Recommended)**: Uses the improved RandomDateAssetsPool for all assets
  - Provides balanced selection across your entire photo library timeline
  - Eliminates bias toward newer photos
  - Ensures truly random chronological distribution
- **`> 0`**: Mixed mode - uses chronological selection for specified count, then falls back to other methods
  - May result in less balanced temporal distribution

#### Video Configuration Options

Configure video playback with these settings:

```json
{
  "ShowVideos": true,
  "ShowVideosOnly": false
}
```

**Setting Combinations:**

| ShowVideos | ShowVideosOnly | Result |
|------------|----------------|---------|
| `true` | `false` | **Mixed Mode** - Shows both images and videos in slideshow |
| `true` | `true` | **Videos Only** - Shows only video assets |
| `false` | `false` | **Images Only** - Shows only image assets (default behavior) |
| `false` | `true` | ❌ **Invalid** - Cannot show videos only when videos are disabled |

**Recommended Settings:**
- **For mixed slideshows**: `"ShowVideos": true, "ShowVideosOnly": false`
- **For video-only digital frames**: `"ShowVideos": true, "ShowVideosOnly": true`
- **For traditional photo frames**: `"ShowVideos": false, "ShowVideosOnly": false`

#### Settings Ignored with ChronologicalImagesCount > 0

> **Important**: When using `ChronologicalImagesCount > 0`, the following filter settings are **ignored** for the chronological portion:

```json
{
  "ImagesFromDate": null,          // ❌ Ignored - chronological algorithm handles date ranges
  "ShowMemories": false,           // ❌ Ignored - memories filtering not applied
  "ShowFavorites": false,          // ❌ Ignored - favorites filtering not applied  
  "ShowArchived": false,           // ❌ Ignored - archive filtering not applied
  "ImagesFromDays": null,          // ❌ Ignored - relative date filtering not applied
  "ImagesUntilDate": "2020-01-02", // ❌ Ignored - end date filtering not applied
  "ChronologicalImagesCount": 100   // ✅ Used - specifies how many assets use chronological selection
}
```

**Why These Are Ignored:**
- The chronological algorithm (`RandomDateAssetsPool`) has its own balanced selection logic
- It creates temporal clusters and selects across your entire library timeline
- External filters would interfere with the balanced chronological distribution
- After chronological assets are selected, remaining assets can use these filters

#### Compatible Feature Combinations

**✅ Works Well Together:**
- `ChronologicalImagesCount: 0` + All filter settings + `ShowVideos: true` + Dynamic Color Adaptation
- `ChronologicalImagesCount: 0` + `ShowMemories: true` + `ShowFavorites: true`
- Chronological selection applies to both images and videos
- Color extraction works for both asset types

**⚠️ Mixed Mode Behavior (`ChronologicalImagesCount > 0`):**
- First N assets: Selected chronologically (filters ignored)
- Remaining assets: Use standard selection with all filter settings applied
- May result in inconsistent filtering across the slideshow

**🎯 Recommended Approach:**
- Use `ChronologicalImagesCount: 0` for consistent behavior across all assets
- Apply filters like `ShowMemories`, `ShowFavorites`, etc. normally
- This ensures all your filter preferences are respected



## 🖼️ Demo

You can find a working demo [here](https://demo.immichframe.dev).

<img src="/design/demo/web_demo.png" alt="Web Demo">

## ⚠️ Disclaimer

**This project is not affiliated with [immich][immich-github-url]!**

## 📜 License

[GNU General Public License v3.0](LICENSE.txt)

## 🆘 Help

[Discord Channel][support-url]

## 🙏 Acknowledgments

- BIG thanks to the [immich team][immich-github-url] for creating an awesome tool

## 🌟 Star History

[![Star History Chart](https://api.star-history.com/svg?repos=immichframe/immichframe&type=Date)](https://www.star-history.com/#immichframe/immichframe&Date)

<!-- MARKDOWN LINKS & IMAGES -->
<!-- https://www.markdownguide.org/basic-syntax/#reference-style-links -->

[contributors-shield]: https://img.shields.io/github/contributors/immichFrame/ImmichFrame.svg?style=for-the-badge
[contributors-url]: https://github.com/immichFrame/ImmichFrame/graphs/contributors
[forks-shield]: https://img.shields.io/github/forks/immichFrame/ImmichFrame.svg?style=for-the-badge
[forks-url]: https://github.com/immichFrame/ImmichFrame/network/members
[stars-shield]: https://img.shields.io/github/stars/immichFrame/ImmichFrame.svg?style=for-the-badge
[stars-url]: https://github.com/immichFrame/ImmichFrame/stargazers
[issues-shield]: https://img.shields.io/github/issues/immichFrame/ImmichFrame.svg?style=for-the-badge
[issues-url]: https://github.com/immichFrame/ImmichFrame/issues
[license-shield]: https://img.shields.io/github/license/immichFrame/ImmichFrame.svg?style=for-the-badge
[license-url]: https://github.com/immichFrame/ImmichFrame/blob/master/LICENSE.txt
[releases-url]: https://github.com/immichFrame/ImmichFrame/releases/latest
[support-url]: https://discord.com/channels/979116623879368755/1217843270244372480
[openweathermap-url]: https://openweathermap.org/
[immich-github-url]: https://github.com/immich-app/immich
[immich-api-url]: https://immich.app/docs/features/command-line-interface#obtain-the-api-key
