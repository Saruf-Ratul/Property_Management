/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        brand: {
          50: '#eef5ff',
          100: '#d9e7ff',
          200: '#bcd5ff',
          300: '#8ebaff',
          400: '#5994ff',
          500: '#356eff',
          600: '#1f4ce8',
          700: '#1a3cc1',
          800: '#1c349c',
          900: '#1c317c',
          950: '#161f4b',
        },
      },
      fontFamily: {
        sans: ['Inter', 'ui-sans-serif', 'system-ui', 'sans-serif'],
      },
      boxShadow: {
        soft: '0 1px 2px 0 rgba(15, 23, 42, 0.04), 0 4px 12px -2px rgba(15, 23, 42, 0.06)',
      },
    },
  },
  plugins: [],
}
