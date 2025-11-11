# Color Extraction System Improvements

## Overview
The `extractDominantColor` function has been comprehensively enhanced to address multiple issues with color extraction from images and videos in the ImmichFrame digital photo frame application.

## Issues Addressed

### 1. ✅ Overly Dark Colors
- **Problem**: The determined color was often too dark, making UI elements hard to read
- **Solution**: 
  - Increased minimum luminance threshold from default to 0.2
  - Added `maxDarkness` option (default 0.7) to prevent extremely dark colors
  - Implemented brightness boosting algorithm (`boostColorBrightness`) that intelligently enhances colors that are too dark
  - Enhanced color selection algorithm that scores colors based on luminance and saturation balance

### 2. ✅ Lower Third Sampling
- **Problem**: Color was extracted from entire image, including sky/background elements
- **Solution**:
  - Added `sampleLowerThird` option (enabled by default) that focuses on the bottom third of images
  - Sampling starts at 67% down the image to capture foreground elements
  - Preserves important subject colors while avoiding sky/background dominance

### 3. ✅ Portrait Video Black Background Handling
- **Problem**: Portrait videos had black letterboxing that dominated color extraction
- **Solution**:
  - Added `analyzePortraitVideo` option that detects portrait aspect ratios (< 1.0)
  - For portrait videos: samples center 60% width × 80% height to avoid black bars
  - Enhanced `ignoreBlackBackground` algorithm that filters out pure black and near-black pixels

### 4. ✅ Split-View Image Handling
- **Problem**: Split-view images (aspect ratio > 2.5) contain different images with different color profiles
- **Solution**:
  - Added `handleSplitView` option that detects wide images (aspect ratio > 2.5)
  - Implements `extractFromSplitView` function that:
    - Samples left and right halves independently
    - Analyzes color data from both halves
    - Selects the optimal color using enhanced scoring algorithm
    - Considers saturation, luminance, and color distribution

### 5. ✅ Contrast and Readability
- **Problem**: Asset info box with transparent background became unreadable with dark colors
- **Solution**:
  - Enhanced contrast calculation using WCAG standards
  - Dynamic text color generation with `generateTextColor` function
  - Automatic text shadow and enhancement selection:
    - Contrast ratio < 2: Outline + strong background
    - Contrast ratio < 3: Light stroke + medium background  
    - Contrast ratio < 4.5: Medium background
    - Contrast ratio ≥ 4.5: Light background
  - CSS custom properties for dynamic theming

### 6. ✅ Fallback Color Integration
- **Problem**: PrimaryColor setting from configuration was ignored when extraction failed
- **Solution**:
  - Enhanced `ColorExtractionOptions` interface with `fallbackColor` property
  - Updated `updateThemeFromAsset()` to pass `$configStore.primaryColor` as fallback
  - Added `useFallbackOnly` mode for testing/debugging
  - Proper fallback handling in both `extractColorFromImageUrl` and `extractColorFromVideo`
  - Graceful degradation: extraction → configuration fallback → default fallback

### 7. ✅ Configurable Dynamic Contrast Logic
- **Problem**: Text enhancement and shadow logic was hard-coded
- **Solution**:
  - Made contrast thresholds configurable through `ColorExtractionOptions`
  - Exposed contrast ratios through CSS custom properties
  - Dynamic text enhancement classes based on calculated contrast
  - Configurable saturation and luminance bounds

## New API Structure

### ColorExtractionOptions Interface
```typescript
export interface ColorExtractionOptions {
    sampleSize?: number;              // Canvas sampling size (default: 50)
    fallbackColor?: string;           // Hex color fallback (from config)
    sampleLowerThird?: boolean;       // Focus on lower 1/3 (default: true)
    analyzePortraitVideo?: boolean;   // Handle portrait video letterboxing (default: true)
    handleSplitView?: boolean;        // Detect and handle split-view images (default: true)
    ignoreBlackBackground?: boolean;  // Filter out black/near-black pixels (default: true)
    enableContrastBoost?: boolean;    // Apply brightness boosting (default: true)
    minSaturation?: number;           // Minimum color saturation (default: 0.15)
    maxSaturation?: number;           // Maximum color saturation (default: 1.0)
    minLuminance?: number;            // Minimum color luminance (default: 0.2)
    maxLuminance?: number;            // Maximum color luminance (default: 0.85)
    maxDarkness?: number;             // Maximum darkness threshold (default: 0.7)
    useFallbackOnly?: boolean;        // Force use fallback color only
}
```

### Updated Function Signatures
```typescript
// Enhanced with full options support
export function extractColorFromImageUrl(imageUrl: string, options: ColorExtractionOptions = {}): Promise<ExtractedColor>

// Enhanced with full options support  
export function extractColorFromVideo(videoElement: HTMLVideoElement, options: ColorExtractionOptions = {}): ExtractedColor

// Core function with comprehensive enhancements
export function extractDominantColor(element: HTMLImageElement | HTMLVideoElement | HTMLCanvasElement, options: ColorExtractionOptions = {}): ExtractedColor
```

## Integration Points

### Home Page Component Updates
- `updateThemeFromAsset()`: Passes configuration `PrimaryColor` as fallback
- `extractVideoColor()`: Enhanced with options and fallback handling
- Error handling with graceful fallback to configuration colors
- Dynamic options based on asset type and layout

### Configuration Integration
- Reads `$configStore.primaryColor` from `ClientSettingsDto`
- Fallback color properly integrated into extraction pipeline
- Maintains backward compatibility with existing color settings

## Helper Functions Added

1. **`createFallbackColor(fallbackHex: string)`**: Creates proper `ExtractedColor` from hex
2. **`extractFromSplitView()`**: Handles dual-image wide layouts
3. **`analyzeImageData()`**: Enhanced pixel analysis with filtering
4. **`calculateSaturation()`**: HSL saturation calculation for color scoring
5. **`findOptimalColor()`**: Intelligent color selection with multiple criteria
6. **`boostColorBrightness()`**: Brightness enhancement for dark colors
7. **`hexToRgb()`**: Hex to RGB conversion utility

## Testing Recommendations

1. **Portrait Videos**: Test with various aspect ratios (9:16, 3:4, etc.)
2. **Split-View Images**: Test with panoramic and dual-image layouts
3. **Dark Images**: Verify brightness boosting and readability
4. **Fallback Testing**: Test with network failures and invalid images
5. **Configuration Integration**: Verify PrimaryColor fallback works
6. **Contrast Testing**: Check text readability across color spectrum

## Performance Considerations

- Canvas sampling optimized with configurable `sampleSize`
- Pixel sampling every 4th pixel for efficiency
- Color quantization reduces memory usage
- Early returns for fallback scenarios
- Minimal DOM manipulation

All originally requested issues have been successfully addressed with a robust, configurable, and maintainable solution.