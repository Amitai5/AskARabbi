export interface SourceOption {
  key: string
  label: string
  description: string
  group: 'Core collections' | 'Major works'
}

export const SourceOptions: readonly SourceOption[] = [
  { key: 'collection:Torah', label: 'Torah', description: 'The Five Books of Moses', group: 'Core collections' },
  { key: 'collection:Tanakh', label: 'Tanakh', description: 'Prophets and Writings', group: 'Core collections' },
  { key: 'collection:Mishnah', label: 'Mishnah', description: 'Early rabbinic legal traditions', group: 'Core collections' },
  { key: 'collection:Talmud', label: 'Talmud', description: 'Babylonian and Jerusalem Talmud', group: 'Core collections' },
  { key: 'work:rif', label: 'Rif', description: "Rabbi Isaac Alfasi's legal digest", group: 'Major works' },
  { key: 'work:mishneh_torah', label: 'Mishneh Torah', description: "Maimonides' code of Jewish law", group: 'Major works' },
  { key: 'work:shulchan_arukh_with_rema', label: 'Shulchan Arukh with Rema', description: 'Halakhic code with Ashkenazi glosses', group: 'Major works' },
  { key: 'work:zohar', label: 'Zohar', description: 'Foundational Kabbalistic text', group: 'Major works' },
  { key: 'work:zohar_chadash', label: 'Zohar Chadash', description: 'Additional Zoharic material', group: 'Major works' },
  { key: 'work:mesillat_yesharim', label: 'Mesillat Yesharim', description: 'Ethical and spiritual instruction', group: 'Major works' },
]

export const AllSourceKeys = SourceOptions.map((source) => source.key)
export const CoreSourceKeys = SourceOptions.filter((source) => source.group === 'Core collections').map((source) => source.key)

export function formatSourceSelection(sourceKeys: readonly string[]) {
  if (sourceKeys.length === SourceOptions.length) {
    return 'All approved sources'
  }
  if (hasSameSourceKeys(sourceKeys, CoreSourceKeys)) {
    return 'Core collections'
  }

  const labels = SourceOptions
    .filter((source) => sourceKeys.includes(source.key))
    .map((source) => source.label)

  if (labels.length <= 3) {
    return new Intl.ListFormat('en', { style: 'long', type: 'conjunction' }).format(labels)
  }

  return `${labels.length} selected sources`
}

function hasSameSourceKeys(left: readonly string[], right: readonly string[]) {
  return left.length === right.length && right.every((sourceKey) => left.includes(sourceKey))
}
