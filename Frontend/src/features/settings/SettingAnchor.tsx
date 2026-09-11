import type { ReactNode } from 'react'
import { settingDefinition } from './settingsRegistry.ts'

export function SettingAnchor({ id, children }: { id: string; children: ReactNode }) {
  const setting = settingDefinition(id)
  return <div id={`setting-${id}`} data-setting={id} data-setting-section={setting.section} tabIndex={-1} className="settings-target">{children}</div>
}
