import { createContext, useContext } from 'react';

interface ThemeCtx {
  theme: 'light' | 'dark';
  toggle: () => void;
}

export const ThemeContext = createContext<ThemeCtx>({ theme: 'light', toggle: () => {} });
export const useThemeContext = () => useContext(ThemeContext);
