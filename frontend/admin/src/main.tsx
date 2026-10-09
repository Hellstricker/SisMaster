import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './App.tsx'
import { instalarFetchAutenticado } from './auth/instalarFetch'

// Toda chamada a /api/ passa a levar o token da sessão (e um 401 leva ao login).
instalarFetchAutenticado()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
