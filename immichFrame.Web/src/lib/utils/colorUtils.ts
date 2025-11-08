/**
 * Color utility functions for calculating complementary and contrasting colors
 */

/**
 * Convert hex color to RGB values
 */
export function hexToRgb(hex: string): { r: number; g: number; b: number } | null {
    const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
    return result
        ? {
              r: parseInt(result[1], 16),
              g: parseInt(result[2], 16),
              b: parseInt(result[3], 16)
          }
        : null;
}

/**
 * Convert RGB to hex
 */
export function rgbToHex(r: number, g: number, b: number): string {
    return '#' + [r, g, b].map(x => {
        const hex = x.toString(16);
        return hex.length === 1 ? '0' + hex : hex;
    }).join('');
}

/**
 * Calculate relative luminance of a color (for WCAG contrast calculations)
 */
export function getLuminance(r: number, g: number, b: number): number {
    const [rs, gs, bs] = [r, g, b].map(c => {
        c = c / 255;
        return c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
    });
    return 0.2126 * rs + 0.7152 * gs + 0.0722 * bs;
}

/**
 * Calculate contrast ratio between two colors
 */
export function getContrastRatio(color1: { r: number; g: number; b: number }, color2: { r: number; g: number; b: number }): number {
    const lum1 = getLuminance(color1.r, color1.g, color1.b);
    const lum2 = getLuminance(color2.r, color2.g, color2.b);
    const brightest = Math.max(lum1, lum2);
    const darkest = Math.min(lum1, lum2);
    return (brightest + 0.05) / (darkest + 0.05);
}

/**
 * Check if a color is considered "dark" (luminance < 0.5)
 */
export function isDarkColor(r: number, g: number, b: number): boolean {
    const luminance = getLuminance(r, g, b);
    return luminance < 0.5;
}

/**
 * Generate a high-contrast complementary color for text that will be readable against backgrounds
 * This creates a color that provides good contrast while still being related to the original
 */
export function generateComplementaryTextColor(primaryHex: string): string {
    const rgb = hexToRgb(primaryHex);
    if (!rgb) {
        // Fallback to white if hex parsing fails
        return '#ffffff';
    }

    const { r, g, b } = rgb;
    const isOriginalDark = isDarkColor(r, g, b);
    
    // Calculate complementary hue (opposite on color wheel)
    // Convert RGB to HSL first
    const max = Math.max(r, g, b) / 255;
    const min = Math.min(r, g, b) / 255;
    const diff = max - min;
    
    let h = 0;
    if (diff !== 0) {
        switch (max) {
            case r / 255:
                h = ((g / 255 - b / 255) / diff + (g < b ? 6 : 0)) / 6;
                break;
            case g / 255:
                h = ((b / 255 - r / 255) / diff + 2) / 6;
                break;
            case b / 255:
                h = ((r / 255 - g / 255) / diff + 4) / 6;
                break;
        }
    }
    
    // Complement is 180 degrees opposite
    const complementaryH = (h + 0.5) % 1;
    
    // For text, we want high contrast - so if original is dark, make complement light and vice versa
    const s = 0.8; // High saturation for vibrant color
    const l = isOriginalDark ? 0.85 : 0.2; // Light text for dark bg, dark text for light bg
    
    // Convert HSL back to RGB
    const c = (1 - Math.abs(2 * l - 1)) * s;
    const x = c * (1 - Math.abs(((complementaryH * 6) % 2) - 1));
    const m = l - c / 2;
    
    let rNew = 0, gNew = 0, bNew = 0;
    const hue = complementaryH * 6;
    
    if (hue < 1) {
        [rNew, gNew, bNew] = [c, x, 0];
    } else if (hue < 2) {
        [rNew, gNew, bNew] = [x, c, 0];
    } else if (hue < 3) {
        [rNew, gNew, bNew] = [0, c, x];
    } else if (hue < 4) {
        [rNew, gNew, bNew] = [0, x, c];
    } else if (hue < 5) {
        [rNew, gNew, bNew] = [x, 0, c];
    } else {
        [rNew, gNew, bNew] = [c, 0, x];
    }
    
    rNew = Math.round((rNew + m) * 255);
    gNew = Math.round((gNew + m) * 255);
    bNew = Math.round((bNew + m) * 255);
    
    // Ensure we have enough contrast - if not, fall back to black or white
    const contrastRatio = getContrastRatio(rgb, { r: rNew, g: gNew, b: bNew });
    
    if (contrastRatio < 4.5) { // WCAG AA standard
        return isOriginalDark ? '#ffffff' : '#000000';
    }
    
    return rgbToHex(rNew, gNew, bNew);
}

/**
 * Simple function to get a high-contrast color (black or white) based on background
 */
export function getHighContrastColor(backgroundHex: string): string {
    const rgb = hexToRgb(backgroundHex);
    if (!rgb) {
        return '#ffffff';
    }
    
    return isDarkColor(rgb.r, rgb.g, rgb.b) ? '#ffffff' : '#000000';
}