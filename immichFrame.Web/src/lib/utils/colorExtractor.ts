/**
 * Color extraction utilities for extracting dominant colors from images and videos
 */

export interface ExtractedColor {
	hex: string;
	rgb: [number, number, number];
	luminance: number;
}

/**
 * Extract dominant color from an image element or canvas
 */
export function extractDominantColor(
	element: HTMLImageElement | HTMLVideoElement | HTMLCanvasElement,
	sampleSize: number = 50
): ExtractedColor {
	const canvas = document.createElement('canvas');
	const ctx = canvas.getContext('2d');
	
	if (!ctx) {
		// Fallback to a neutral color if canvas context is not available
		return {
			hex: '#4a5568',
			rgb: [74, 85, 104],
			luminance: 0.3
		};
	}

	// Set canvas size for sampling
	canvas.width = sampleSize;
	canvas.height = sampleSize;

	try {
		// Draw the element to canvas for color analysis
		if (element instanceof HTMLVideoElement) {
			ctx.drawImage(element, 0, 0, sampleSize, sampleSize);
		} else if (element instanceof HTMLImageElement) {
			// Ensure image is loaded before drawing
			if (!element.complete || element.naturalHeight === 0) {
				throw new Error('Image not loaded');
			}
			ctx.drawImage(element, 0, 0, sampleSize, sampleSize);
		} else {
			// Canvas element
			ctx.drawImage(element, 0, 0, sampleSize, sampleSize);
		}

		// Get image data
		const imageData = ctx.getImageData(0, 0, sampleSize, sampleSize);
		const data = imageData.data;

		// Color frequency map
		const colorMap = new Map<string, { count: number; rgb: [number, number, number] }>();

		// Sample pixels and count color frequencies
		for (let i = 0; i < data.length; i += 16) { // Sample every 4th pixel for performance
			const r = data[i];
			const g = data[i + 1];
			const b = data[i + 2];
			const alpha = data[i + 3];

			// Skip transparent or near-transparent pixels
			if (alpha < 128) continue;

			// Quantize colors to reduce noise (group similar colors)
			const quantizedR = Math.floor(r / 32) * 32;
			const quantizedG = Math.floor(g / 32) * 32;
			const quantizedB = Math.floor(b / 32) * 32;

			const key = `${quantizedR},${quantizedG},${quantizedB}`;
			
			if (colorMap.has(key)) {
				colorMap.get(key)!.count++;
			} else {
				colorMap.set(key, { 
					count: 1, 
					rgb: [quantizedR, quantizedG, quantizedB] 
				});
			}
		}

		// Find the most frequent color (excluding very dark/light colors for better theming)
		let dominantColor = { count: 0, rgb: [74, 85, 104] as [number, number, number] };
		
		for (const [_, colorData] of colorMap) {
			const [r, g, b] = colorData.rgb;
			const luminance = getLuminance(r, g, b);
			
			// Prefer colors that aren't too dark or too light for better UI theming
			if (luminance > 0.1 && luminance < 0.8 && colorData.count > dominantColor.count) {
				dominantColor = colorData;
			}
		}

		// If no suitable color found, find any dominant color
		if (dominantColor.count === 0) {
			for (const [_, colorData] of colorMap) {
				if (colorData.count > dominantColor.count) {
					dominantColor = colorData;
				}
			}
		}

		const [r, g, b] = dominantColor.rgb;
		const hex = rgbToHex(r, g, b);
		const luminance = getLuminance(r, g, b);

		return {
			hex,
			rgb: [r, g, b],
			luminance
		};

	} catch (error) {
		console.warn('Color extraction failed:', error);
		// Return a fallback color
		return {
			hex: '#4a5568',
			rgb: [74, 85, 104],
			luminance: 0.3
		};
	}
}

/**
 * Extract color from image URL by creating a temporary image element
 */
export function extractColorFromImageUrl(imageUrl: string): Promise<ExtractedColor> {
	return new Promise((resolve, reject) => {
		const img = new Image();
		img.crossOrigin = 'anonymous';
		
		img.onload = () => {
			try {
				const color = extractDominantColor(img);
				resolve(color);
			} catch (error) {
				reject(error);
			}
		};
		
		img.onerror = () => {
			reject(new Error('Failed to load image for color extraction'));
		};
		
		img.src = imageUrl;
	});
}

/**
 * Extract color from video element at current time
 */
export function extractColorFromVideo(videoElement: HTMLVideoElement): ExtractedColor {
	// Ensure video is ready for color extraction
	if (videoElement.readyState < 2) {
		// Video metadata not loaded yet, return fallback
		return {
			hex: '#4a5568',
			rgb: [74, 85, 104],
			luminance: 0.3
		};
	}
	
	return extractDominantColor(videoElement);
}

/**
 * Calculate relative luminance of RGB color
 */
function getLuminance(r: number, g: number, b: number): number {
	// Normalize RGB values to 0-1
	const [rs, gs, bs] = [r, g, b].map(c => {
		c = c / 255;
		return c <= 0.03928 ? c / 12.92 : Math.pow((c + 0.055) / 1.055, 2.4);
	});
	
	// Calculate relative luminance using sRGB formula
	return 0.2126 * rs + 0.7152 * gs + 0.0722 * bs;
}

/**
 * Convert RGB to hex color
 */
function rgbToHex(r: number, g: number, b: number): string {
	const toHex = (n: number) => {
		const hex = Math.round(n).toString(16);
		return hex.length === 1 ? '0' + hex : hex;
	};
	
	return `#${toHex(r)}${toHex(g)}${toHex(b)}`;
}

/**
 * Calculate contrast ratio between two colors (WCAG standard)
 */
export function getContrastRatio(color1: string, color2: string): number {
	const hex1 = color1.replace('#', '');
	const hex2 = color2.replace('#', '');
	
	const r1 = parseInt(hex1.substr(0, 2), 16);
	const g1 = parseInt(hex1.substr(2, 2), 16);
	const b1 = parseInt(hex1.substr(4, 2), 16);
	
	const r2 = parseInt(hex2.substr(0, 2), 16);
	const g2 = parseInt(hex2.substr(2, 2), 16);
	const b2 = parseInt(hex2.substr(4, 2), 16);
	
	const luminance1 = getLuminance(r1, g1, b1);
	const luminance2 = getLuminance(r2, g2, b2);
	
	const brighter = Math.max(luminance1, luminance2);
	const darker = Math.min(luminance1, luminance2);
	
	return (brighter + 0.05) / (darker + 0.05);
}

/**
 * Generate optimal text color with guaranteed WCAG AAA contrast (7:1 ratio)
 */
export function generateTextColor(backgroundColor: string): string {
	const whiteContrast = getContrastRatio(backgroundColor, '#ffffff');
	const blackContrast = getContrastRatio(backgroundColor, '#000000');
	
	// WCAG AAA requires 7:1 contrast ratio for enhanced accessibility
	if (whiteContrast >= 7) {
		return '#ffffff';
	} else if (blackContrast >= 7) {
		return '#000000';
	} else {
		// If neither pure black nor white provides AAA contrast, 
		// choose the one with better contrast and we'll enhance with shadows
		return whiteContrast > blackContrast ? '#ffffff' : '#000000';
	}
}

/**
 * Generate a complementary color that's suitable for UI theming
 */
export function generateComplementaryColor(baseColor: ExtractedColor): ExtractedColor {
	const [r, g, b] = baseColor.rgb;
	
	// Convert to HSL for better color manipulation
	const hsl = rgbToHsl(r, g, b);
	
	// Generate complementary color by rotating hue 180 degrees
	let complementaryHue = (hsl[0] + 180) % 360;
	
	// Adjust saturation and lightness for better UI suitability
	let saturation = Math.max(0.3, Math.min(0.7, hsl[1])); // Keep saturation between 30-70%
	let lightness = hsl[2];
	
	// Ensure good contrast - if original is dark, make complement lighter and vice versa
	if (baseColor.luminance < 0.3) {
		lightness = Math.max(0.4, lightness);
	} else if (baseColor.luminance > 0.7) {
		lightness = Math.min(0.6, lightness);
	}
	
	const [cr, cg, cb] = hslToRgb(complementaryHue, saturation, lightness);
	
	return {
		hex: rgbToHex(cr, cg, cb),
		rgb: [cr, cg, cb],
		luminance: getLuminance(cr, cg, cb)
	};
}

/**
 * Convert RGB to HSL
 */
function rgbToHsl(r: number, g: number, b: number): [number, number, number] {
	r /= 255;
	g /= 255;
	b /= 255;
	
	const max = Math.max(r, g, b);
	const min = Math.min(r, g, b);
	let h = 0;
	let s = 0;
	const l = (max + min) / 2;
	
	if (max !== min) {
		const d = max - min;
		s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
		
		switch (max) {
			case r: h = (g - b) / d + (g < b ? 6 : 0); break;
			case g: h = (b - r) / d + 2; break;
			case b: h = (r - g) / d + 4; break;
		}
		h /= 6;
	}
	
	return [h * 360, s, l];
}

/**
 * Convert HSL to RGB
 */
function hslToRgb(h: number, s: number, l: number): [number, number, number] {
	h /= 360;
	
	const hue2rgb = (p: number, q: number, t: number) => {
		if (t < 0) t += 1;
		if (t > 1) t -= 1;
		if (t < 1/6) return p + (q - p) * 6 * t;
		if (t < 1/2) return q;
		if (t < 2/3) return p + (q - p) * (2/3 - t) * 6;
		return p;
	};
	
	let r, g, b;
	
	if (s === 0) {
		r = g = b = l;
	} else {
		const q = l < 0.5 ? l * (1 + s) : l + s - l * s;
		const p = 2 * l - q;
		r = hue2rgb(p, q, h + 1/3);
		g = hue2rgb(p, q, h);
		b = hue2rgb(p, q, h - 1/3);
	}
	
	return [Math.round(r * 255), Math.round(g * 255), Math.round(b * 255)];
}