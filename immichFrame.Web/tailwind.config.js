/** @type {import('tailwindcss').Config} */

import screenSafePlugin from './tailwind.plugins';

export default {
  mode: 'jit',
  content: ['./src/**/*.{html,js,svelte,ts}'],
  theme: {
    extend: {
      colors: {
        primary: 'var(--primary-color)',
        secondary: 'var(--secondary-color)',
        complementary: 'var(--complementary-color)',
      },
      textShadow: {
        sm: '1px 1px 2px rgba(0, 0, 0, 0.9)', // Light shadow for small text
        md: '2px 2px 4px rgba(0, 0, 0, 0.9)', // Medium shadow for regular text  
        lg: '3px 3px 6px rgba(0, 0, 0, 0.9)', // Strong shadow for large text
        xl: '4px 4px 8px rgba(0, 0, 0, 0.95)', // Extra strong for poor contrast
        outline: '-1px -1px 0 rgba(0,0,0,0.9), 1px -1px 0 rgba(0,0,0,0.9), -1px 1px 0 rgba(0,0,0,0.9), 1px 1px 0 rgba(0,0,0,0.9)', // Crisp outline for small text
        'stroke-light': '-1px -1px 0 rgba(0,0,0,0.7), 1px -1px 0 rgba(0,0,0,0.7), -1px 1px 0 rgba(0,0,0,0.7), 1px 1px 0 rgba(0,0,0,0.7)', // Light stroke
        'stroke-white': '-1px -1px 0 rgba(255,255,255,0.9), 1px -1px 0 rgba(255,255,255,0.9), -1px 1px 0 rgba(255,255,255,0.9), 1px 1px 0 rgba(255,255,255,0.9)', // White outline
      },
    },
  },
  plugins: [
    function ({ addUtilities, theme, e }) {
      const textShadows = theme('textShadow');
      const textShadowUtilities = Object.keys(textShadows).reduce((acc, key) => {
        acc[`.${e(`text-shadow-${key}`)}`] = {
          textShadow: textShadows[key],
        };
        return acc;
      }, {});

      addUtilities(textShadowUtilities);
    },
    screenSafePlugin
  ],
};
