import { renderToStaticMarkup } from 'react-dom/server'
import { App } from './App.tsx'

export function render() {
  return renderToStaticMarkup(<App />)
}
