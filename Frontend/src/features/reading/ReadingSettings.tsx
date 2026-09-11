import { useReadingPreferences } from './readingContext.ts'
import { LineSpacings, TextSizes, Themes } from './readingPreferences.ts'
import { SettingAnchor } from '../settings/SettingAnchor.tsx'
import { settingDefinition } from '../settings/settingsRegistry.ts'

export function ReadingSettings() {
  const { preferences, update, status, error, retry } = useReadingPreferences()
  const focus = settingDefinition('focused-reading')
  return <div className="space-y-8">
    <p className="text-muted">Changes apply immediately and save automatically to your account.</p>
    <Preset id="text-size" options={TextSizes} selected={preferences.textSize} onChange={textSize => update({ textSize })} />
    <Preset id="line-spacing" options={LineSpacings} selected={preferences.lineSpacing} onChange={lineSpacing => update({ lineSpacing })} />
    <Preset id="theme" options={Themes} selected={preferences.theme} onChange={theme => update({ theme })} />
    <SettingAnchor id="focused-reading"><div className="flex items-start justify-between gap-5"><div><h2 className="font-semibold text-ink">{focus.label}</h2><p id="focused-reading-description" className="mt-1 text-muted">{focus.description}</p><p className="mt-2 text-sm text-muted">You can also choose Focus on any long answer or teaching, and exit at any time.</p></div><button type="button" role="switch" aria-label={focus.label} aria-describedby="focused-reading-description" aria-checked={preferences.focusLongContent} onClick={() => update({ focusLongContent: !preferences.focusLongContent })} className={`relative mt-1 h-7 w-12 shrink-0 rounded-full ${preferences.focusLongContent ? 'bg-pomegranate' : 'bg-stone-deep'}`}><span className={`absolute left-0 top-1 size-5 rounded-full bg-paper shadow-sm transition-transform ${preferences.focusLongContent ? 'translate-x-6' : 'translate-x-1'}`} /></button></div></SettingAnchor>
    <section aria-label="Reading preview" className="rounded-2xl border border-line bg-paper p-5 sm:p-7"><p className="mb-4 text-sm font-semibold uppercase tracking-widest text-muted">Live preview</p><div className="reading-content space-y-4 border-l-2 border-pomegranate pl-5 text-ink-soft"><p>Learning begins with a question. A familiar passage can offer something new when we pause, read closely, and make room for another perspective.</p><p>Choose a comfortable text size and rhythm. Your answers and weekly teachings will use these preferences; buttons and navigation will stay the same size.</p></div></section>
    <div role="status" className="text-sm text-muted">{error ?? (status === 'saving' ? 'Saving reading preferences…' : status === 'loading' ? 'Loading account preferences…' : 'Reading preferences saved.')} {error ? <button type="button" onClick={retry} className="ml-2 min-h-11 font-semibold text-pomegranate underline">Retry</button> : null}</div>
  </div>
}

function Preset<T extends string>({ id, options, selected, onChange }: { id: string; options: Record<T, string>; selected: T; onChange(value: T): void }) {
  const definition = settingDefinition(id)
  return <SettingAnchor id={id}><fieldset aria-describedby={`${id}-description`}><legend className="font-semibold text-ink">{definition.label}</legend><p id={`${id}-description`} className="mt-1 text-muted">{definition.description}</p><div className="mt-3 flex flex-wrap gap-2">{(Object.keys(options) as T[]).map(value => <label key={value} className={`relative cursor-pointer rounded-lg border px-4 py-2.5 text-center text-base transition focus-within:outline-2 focus-within:outline-offset-2 focus-within:outline-pomegranate ${selected === value ? 'border-pomegranate bg-pomegranate/8 font-semibold text-ink' : 'border-line-strong bg-paper text-ink-soft hover:bg-stone'}`}><input type="radio" name={id} value={value} checked={selected === value} onChange={() => onChange(value)} className="sr-only" />{options[value]}</label>)}</div></fieldset></SettingAnchor>
}
