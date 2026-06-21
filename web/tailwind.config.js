/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{js,ts,jsx,tsx}'],
  darkMode: 'class',
  theme: {
    extend: {
      colors: {
        accent: {
          DEFAULT: '#3DD6B5',
          from: '#3DD6B5',
          to: '#2FB8A0',
        },
        ink: {
          bg: 'rgb(var(--ink-bg) / <alpha-value>)',
          panel: 'rgb(var(--ink-panel) / <alpha-value>)',
          border: 'rgb(var(--ink-border) / <alpha-value>)',
          text: 'rgb(var(--ink-text) / <alpha-value>)',
          dim: 'rgb(var(--ink-dim) / <alpha-value>)',
        },
      },
      boxShadow: {
        soft: '0 4px 24px -4px rgba(0,0,0,0.45)',
        glow: '0 0 0 1px rgba(61,214,181,0.25), 0 8px 28px -6px rgba(61,214,181,0.25)',
      },
      borderRadius: {
        xl: '0.85rem',
      },
      keyframes: {
        pulseSoft: {
          '0%, 100%': { opacity: '1' },
          '50%': { opacity: '0.55' },
        },
        fadeIn: {
          '0%': { opacity: '0', transform: 'translateY(4px)' },
          '100%': { opacity: '1', transform: 'translateY(0)' },
        },
      },
      animation: {
        pulseSoft: 'pulseSoft 2.4s ease-in-out infinite',
        fadeIn: 'fadeIn 0.25s ease-out',
      },
    },
  },
  plugins: [],
}
