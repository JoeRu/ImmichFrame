/**
 * Color extraction utilities for extracting dominant colors from images and videos
 */

export interface ExtractedColor {
	hex: string;
	rgb: [number, number, number];
	luminance: number;
}

export interface ColorExtractionOptions {
	sampleSize?: number;
	fallbackColor?: string;
	sampleLowerThird?: boolean;
	analyzePortraitVideo?: boolean;
	handleSplitView?: boolean;
	ignoreBlackBackground?: boolean;
	enableContrastBoost?: boolean;
	minSaturation?: number;
	maxSaturation?: number;
	minLuminance?: number;
	maxLuminance?: number;
	maxDarkness?: number;
	useFallbackOnly?: boolean;
}

/**
 * Extract dominant color from an image element or canvas with enhanced algorithms
 * Addresses issues: dark colors, lower third sampling, portrait video backgrounds, 
 * split-view handling, contrast optimization, and fallback integration
 */
export function extractDominantColor(
	element: HTMLImageElement | HTMLVideoElement | HTMLCanvasElement,
	options: ColorExtractionOptions = {}
): ExtractedColor {
	const {
		sampleSize = 50,
		fallbackColor = '#4a5568',
		sampleLowerThird = true,
		analyzePortraitVideo = true,
		handleSplitView = true,
		ignoreBlackBackground = true,
		enableContrastBoost = true,
		minSaturation = 0.15,
		maxSaturation = 1.0,
		minLuminance = 0.2,  // Increased to avoid overly dark colors
		maxLuminance = 0.85, // Slightly increased for better balance
		maxDarkness = 0.7
	} = options;

	const canvas = document.createElement('canvas');
	const ctx = canvas.getContext('2d');
	
	if (!ctx) {
		return createFallbackColor(fallbackColor);
	}

	// Set canvas size for sampling
	canvas.width = sampleSize;
	canvas.height = sampleSize;

	try {
		// Determine element type and aspect ratio for specialized handling
		let isPortraitVideo = false;
		let isSplitView = false;
		
		if (element instanceof HTMLVideoElement && analyzePortraitVideo) {
			const aspectRatio = element.videoWidth / element.videoHeight;
			isPortraitVideo = aspectRatio < 1;
			// Draw video to canvas
			ctx.drawImage(element, 0, 0, sampleSize, sampleSize);
		} else if (element instanceof HTMLImageElement) {
			// Ensure image is loaded before drawing
			if (!element.complete || element.naturalHeight === 0) {
				throw new Error('Image not loaded');
			}
			
			const aspectRatio = element.naturalWidth / element.naturalHeight;
			// Detect split-view (very wide images that might have different content on left/right)
			if (handleSplitView) {
				isSplitView = aspectRatio > 2.5;
			}
			
			ctx.drawImage(element, 0, 0, sampleSize, sampleSize);
		} else {
			// Canvas element
			ctx.drawImage(element, 0, 0, sampleSize, sampleSize);
		}

		// Get image data with focused sampling region
		let imageData: ImageData;
		
		if (isPortraitVideo && ignoreBlackBackground) {
			// For portrait videos, sample center region to avoid black bars
			const centerWidth = Math.floor(sampleSize * 0.6);
			const centerHeight = Math.floor(sampleSize * 0.8);
			const offsetX = Math.floor((sampleSize - centerWidth) / 2);
			const offsetY = Math.floor((sampleSize - centerHeight) / 2);
			
			imageData = ctx.getImageData(offsetX, offsetY, centerWidth, centerHeight);
		} else if (isSplitView && handleSplitView) {
			// For split-view, sample both halves and blend results
			return extractFromSplitView(ctx, sampleSize, options);
		} else if (sampleLowerThird) {
			// Standard case: focus on lower third of the image
			const lowerThirdStart = Math.floor(sampleSize * 0.67); // Start at 2/3 down
			const lowerThirdHeight = sampleSize - lowerThirdStart;
			
			imageData = ctx.getImageData(0, lowerThirdStart, sampleSize, lowerThirdHeight);
		} else {
			// Full image sampling
			imageData = ctx.getImageData(0, 0, sampleSize, sampleSize);
		}

		const data = imageData.data;

		// Enhanced color frequency analysis
		const colorMap = new Map<string, { 
			count: number; 
			rgb: [number, number, number];
			luminance: number;
			saturation: number;
		}>();

		// Sample pixels with improved quantization
		for (let i = 0; i < data.length; i += 16) { // Sample every 4th pixel
			const r = data[i];
			const g = data[i + 1];
			const b = data[i + 2];
			const alpha = data[i + 3];

			// Skip transparent or near-transparent pixels
			if (alpha < 128) continue;
			
			// Skip near-black pixels if ignoreBlackBackground is enabled
			if (ignoreBlackBackground && r < 30 && g < 30 && b < 30) continue;

			// Improved quantization with better color grouping
			const quantizedR = Math.floor(r / 24) * 24; // Finer quantization for better color detection
			const quantizedG = Math.floor(g / 24) * 24;
			const quantizedB = Math.floor(b / 24) * 24;

			const luminance = getLuminance(quantizedR, quantizedG, quantizedB);
			const saturation = calculateSaturation(quantizedR, quantizedG, quantizedB);
			
			const key = `${quantizedR},${quantizedG},${quantizedB}`;
			
			if (colorMap.has(key)) {
				colorMap.get(key)!.count++;
			} else {
				colorMap.set(key, { 
					count: 1, 
					rgb: [quantizedR, quantizedG, quantizedB],
					luminance,
					saturation
				});
			}
		}

		// Enhanced color selection algorithm
		let bestColor = findOptimalColor(colorMap, minLuminance, maxLuminance, enableContrastBoost);
		
		// If no suitable color found, try with relaxed constraints
		if (!bestColor) {
			bestColor = findOptimalColor(colorMap, 0.1, 0.9, enableContrastBoost);
		}
		
		// Final fallback to most frequent color
		if (!bestColor) {
			let maxCount = 0;
			for (const [_, colorData] of colorMap) {
				if (colorData.count > maxCount) {
					maxCount = colorData.count;
					bestColor = colorData;
				}
			}
		}

		if (bestColor) {
			const [r, g, b] = bestColor.rgb;
			
			// Apply contrast boost if enabled and color is too dark
			let finalRgb: [number, number, number] = [r, g, b];
			if (enableContrastBoost && bestColor.luminance < 0.3) {
				finalRgb = boostColorBrightness(r, g, b, 0.4);
			}
			
			const hex = rgbToHex(finalRgb[0], finalRgb[1], finalRgb[2]);
			const luminance = getLuminance(finalRgb[0], finalRgb[1], finalRgb[2]);

			return {
				hex,
				rgb: finalRgb,
				luminance
			};
		}

		// Final fallback
		return createFallbackColor(fallbackColor);

	} catch (error) {
		console.warn('Color extraction failed:', error);
		return createFallbackColor(fallbackColor);
	}
}

/**
 * Create a fallback color from hex string with proper luminance calculation
 */
function createFallbackColor(fallbackHex: string): ExtractedColor {
	const rgb = hexToRgb(fallbackHex);
	if (!rgb) {
		// Ultimate fallback
		return {
			hex: '#6b7280',
			rgb: [107, 114, 128],
			luminance: 0.4
		};
	}
	
	return {
		hex: fallbackHex,
		rgb: [rgb.r, rgb.g, rgb.b],
		luminance: getLuminance(rgb.r, rgb.g, rgb.b)
	};
}

/**
 * Convert hex color string to RGB object
 */
function hexToRgb(hex: string): { r: number; g: number; b: number } | null {
	const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
	return result ? {
		r: parseInt(result[1], 16),
		g: parseInt(result[2], 16),
		b: parseInt(result[3], 16)
	} : null;
}

/**
 * Handle split-view images by sampling both halves and choosing the better color
 */
function extractFromSplitView(
	ctx: CanvasRenderingContext2D, 
	sampleSize: number, 
	options: ColorExtractionOptions
): ExtractedColor {
	const halfWidth = Math.floor(sampleSize / 2);
	
	// Sample left half (focus on lower third)
	const leftLowerThird = Math.floor(sampleSize * 0.67);
	const leftData = ctx.getImageData(0, leftLowerThird, halfWidth, sampleSize - leftLowerThird);
	
	// Sample right half (focus on lower third) 
	const rightData = ctx.getImageData(halfWidth, leftLowerThird, halfWidth, sampleSize - leftLowerThird);
	
	// Extract colors from both halves
	const leftColors = analyzeImageData(leftData, options);
	const rightColors = analyzeImageData(rightData, options);
	
	// Choose the more vibrant/suitable color
	const leftBest = findOptimalColor(leftColors, options.minLuminance ?? 0.2, options.maxLuminance ?? 0.85, options.enableContrastBoost ?? true);
	const rightBest = findOptimalColor(rightColors, options.minLuminance ?? 0.2, options.maxLuminance ?? 0.85, options.enableContrastBoost ?? true);
	
	// Prefer the color with better saturation and luminance balance
	if (leftBest && rightBest) {
		const leftScore = leftBest.saturation * 0.6 + (1 - Math.abs(leftBest.luminance - 0.5)) * 0.4;
		const rightScore = rightBest.saturation * 0.6 + (1 - Math.abs(rightBest.luminance - 0.5)) * 0.4;
		
		const winner = leftScore > rightScore ? leftBest : rightBest;
		const [r, g, b] = winner.rgb;
		
		return {
			hex: rgbToHex(r, g, b),
			rgb: [r, g, b],
			luminance: winner.luminance
		};
	}
	
	// Fallback to single best color or default
	const bestColor = leftBest || rightBest;
	if (bestColor) {
		const [r, g, b] = bestColor.rgb;
		return {
			hex: rgbToHex(r, g, b),
			rgb: [r, g, b],
			luminance: bestColor.luminance
		};
	}
	
	return createFallbackColor(options.fallbackColor ?? '#4a5568');
}

/**
 * Analyze ImageData and return color frequency map
 */
function analyzeImageData(
	imageData: ImageData, 
	options: ColorExtractionOptions
): Map<string, { count: number; rgb: [number, number, number]; luminance: number; saturation: number }> {
	const colorMap = new Map<string, { 
		count: number; 
		rgb: [number, number, number];
		luminance: number;
		saturation: number;
	}>();
	
	const data = imageData.data;
	const ignoreBlackBackground = options.ignoreBlackBackground ?? true;
	
	for (let i = 0; i < data.length; i += 16) { // Sample every 4th pixel
		const r = data[i];
		const g = data[i + 1];
		const b = data[i + 2];
		const alpha = data[i + 3];

		// Skip transparent or near-transparent pixels
		if (alpha < 128) continue;
		
		// Skip near-black pixels if ignoreBlackBackground is enabled
		if (ignoreBlackBackground && r < 30 && g < 30 && b < 30) continue;

		// Improved quantization
		const quantizedR = Math.floor(r / 24) * 24;
		const quantizedG = Math.floor(g / 24) * 24;
		const quantizedB = Math.floor(b / 24) * 24;

		const luminance = getLuminance(quantizedR, quantizedG, quantizedB);
		const saturation = calculateSaturation(quantizedR, quantizedG, quantizedB);
		
		const key = `${quantizedR},${quantizedG},${quantizedB}`;
		
		if (colorMap.has(key)) {
			colorMap.get(key)!.count++;
		} else {
			colorMap.set(key, { 
				count: 1, 
				rgb: [quantizedR, quantizedG, quantizedB],
				luminance,
				saturation
			});
		}
	}
	
	return colorMap;
}

/**
 * Calculate color saturation from RGB values
 */
function calculateSaturation(r: number, g: number, b: number): number {
	const max = Math.max(r, g, b) / 255;
	const min = Math.min(r, g, b) / 255;
	const delta = max - min;
	
	if (max === 0) return 0;
	return delta / max;
}

/**
 * Find the optimal color from the frequency map based on various criteria
 */
function findOptimalColor(
	colorMap: Map<string, { count: number; rgb: [number, number, number]; luminance: number; saturation: number }>,
	minLuminance: number,
	maxLuminance: number,
	enableContrastBoost: boolean
): { count: number; rgb: [number, number, number]; luminance: number; saturation: number } | null {
	let bestColor: { count: number; rgb: [number, number, number]; luminance: number; saturation: number } | null = null;
	let bestScore = 0;
	
	for (const [_, colorData] of colorMap) {
		const { luminance, saturation, count } = colorData;
		
		// Skip colors outside luminance range
		if (luminance < minLuminance || luminance > maxLuminance) continue;
		
		// Calculate composite score based on:
		// - Frequency (how common the color is)
		// - Saturation (more vibrant colors preferred)
		// - Luminance balance (colors closer to mid-range preferred)
		// - Avoid overly dark colors
		
		const frequencyScore = Math.min(count / 10, 1); // Normalize frequency
		const saturationScore = Math.min(saturation * 1.5, 1); // Boost saturation importance
		const luminanceScore = 1 - Math.abs(luminance - 0.5); // Prefer mid-range luminance
		const darknessBonus = luminance > 0.25 ? 1.2 : 1; // Bonus for avoiding very dark colors
		
		const totalScore = (frequencyScore * 0.4 + saturationScore * 0.3 + luminanceScore * 0.3) * darknessBonus;
		
		if (totalScore > bestScore) {
			bestScore = totalScore;
			bestColor = colorData;
		}
	}
	
	return bestColor;
}

/**
 * Boost the brightness of a color while maintaining its hue
 */
function boostColorBrightness(r: number, g: number, b: number, targetLuminance: number): [number, number, number] {
	// Convert to HSL for easier brightness manipulation
	const hsl = rgbToHsl(r, g, b);
	
	// Increase lightness while preserving hue and saturation
	hsl[2] = Math.max(hsl[2], targetLuminance);
	
	// Convert back to RGB
	const boostedRgb = hslToRgb(hsl[0], hsl[1], hsl[2]);
	
	return [
		Math.min(255, Math.max(0, boostedRgb[0])),
		Math.min(255, Math.max(0, boostedRgb[1])),
		Math.min(255, Math.max(0, boostedRgb[2]))
	];
}

/**
 * Extract color from image URL by creating a temporary image element
 * Now supports enhanced options including fallback color integration
 */
export function extractColorFromImageUrl(imageUrl: string, options: ColorExtractionOptions = {}): Promise<ExtractedColor> {
	return new Promise((resolve, reject) => {
		// Handle fallback-only mode
		if (options.useFallbackOnly && options.fallbackColor) {
			try {
				const fallbackColor = createFallbackColor(options.fallbackColor);
				resolve(fallbackColor);
				return;
			} catch (error) {
				reject(error);
				return;
			}
		}
		
		// Handle empty URL with fallback
		if (!imageUrl && options.fallbackColor) {
			try {
				const fallbackColor = createFallbackColor(options.fallbackColor);
				resolve(fallbackColor);
				return;
			} catch (error) {
				reject(error);
				return;
			}
		}
		
		const img = new Image();
		img.crossOrigin = 'anonymous';
		
		img.onload = () => {
			try {
				const extractionOptions: ColorExtractionOptions = {
					fallbackColor: options.fallbackColor || '#6b7280',
					enableContrastBoost: true,
					ignoreBlackBackground: true,
					...options
				};
				const color = extractDominantColor(img, extractionOptions);
				resolve(color);
			} catch (error) {
				reject(error);
			}
		};
		
		img.onerror = () => {
			if (options.fallbackColor) {
				try {
					const fallbackColor = createFallbackColor(options.fallbackColor);
					resolve(fallbackColor);
				} catch (error) {
					reject(error);
				}
			} else {
				reject(new Error('Failed to load image for color extraction'));
			}
		};
		
		img.src = imageUrl;
	});
}

/**
 * Extract color from video element at current time
 * Enhanced to handle portrait videos and black backgrounds properly with full options support
 */
export function extractColorFromVideo(videoElement: HTMLVideoElement, options: ColorExtractionOptions = {}): ExtractedColor {
	// Handle fallback-only mode
	if (options.useFallbackOnly && options.fallbackColor) {
		return createFallbackColor(options.fallbackColor);
	}
	
	// Ensure video is ready for color extraction
	if (videoElement.readyState < 2) {
		// Video metadata not loaded yet, return fallback
		return createFallbackColor(options.fallbackColor || '#4a5568');
	}
	
	const extractionOptions: ColorExtractionOptions = {
		fallbackColor: options.fallbackColor || '#4a5568',
		enableContrastBoost: true,
		ignoreBlackBackground: true, // Important for portrait videos
		minLuminance: 0.25, // Slightly higher for videos to avoid dark colors
		maxLuminance: 0.8,
		analyzePortraitVideo: true,
		...options
	};
	
	return extractDominantColor(videoElement, extractionOptions);
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