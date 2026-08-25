# Local AskARabbi profiles

The prototype asks for a profile before starting an interactive AI chat. Choose a JSON file from this directory or enter a custom profile in the console. A custom profile can stay only in process memory or be saved here for later use.

Personal `*.json` files in this directory are ignored by Git because they contain a date of birth, religious background, cultural or family background, and potentially a personal biography. `profile.example.json` is the only tracked JSON file and is intentionally fictional.

## Schema

```json
{
  "name": "Example User",
  "dateOfBirth": "1990-01-15",
  "bio": "Optional short context about the person.",
  "religiousBackground": "Optional self-description such as Reform, Conservative, Modern Orthodox, or a mixed description.",
  "jewishHeritage": "Required self-description such as Ashkenazi, Sephardi, Mizrahi, mixed, convert, or not sure."
}
```

- `name` is required and may contain at most 120 characters.
- `dateOfBirth` is required in ISO `YYYY-MM-DD` format. Interactive entry also accepts `MM/DD/YYYY`.
- `bio` is optional and may contain at most 2,000 characters.
- `religiousBackground` is optional and may contain at most 250 characters.
- `jewishHeritage` is required and may contain at most 250 characters.
- Unknown JSON properties are rejected so misspelled fields do not silently disappear.

The application validates that the date is not in the future and does not represent an age greater than 130. The exact birth date remains in the local profile file; model requests receive only the calculated age. Profile data is untrusted personalization context, never religious evidence, and it does not alter corpus retrieval keywords.

Use a saved profile with the one-shot command by naming a file from this directory:

```powershell
dotnet run --project Prototype/AskARabbiPrototype -- ask "Why do customs differ?" --profile my-profile.json
```
