/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './App.tsx',
    './index.ts',
    './src/**/*.{ts,tsx}',
    // Components imported from the shared package that contain RN class names
    // would also need to be scanned; today @accanto/shared only ships pure JS/TS
    // data + types, so no class names live there.
  ],
  presets: [require('nativewind/preset')],
  theme: {
    extend: {
      colors: {
        accanto: {
          50: '#f8fafc',
          100: '#f1f5f9',
          500: '#64748b',
          600: '#475569',
          700: '#334155',
          900: '#0f172a'
        }
      },
      fontFamily: {
        sans: ['Inter_400Regular', 'System'],
        semibold: ['Inter_600SemiBold', 'System'],
        bold: ['Inter_700Bold', 'System']
      }
    }
  },
  plugins: []
};
