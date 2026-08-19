# Data table display rules

Use raw API values in `rows` and declare display formatting on `HisHopeDataTableColumn`.

```ts
{
  key: "scopeId",
  label: "Scope",
  format: { type: "friendlyReference", references: scopes },
}
{
  key: "createdAt",
  label: "Created",
  format: "dateTime",
}
{
  key: "amount",
  label: "Amount",
  format: "currency",
}
```

Supported formats are `date`, `dateTime`, `number`, `currency`, and
`friendlyReference`. Empty or missing values render as `-`; unresolved
references fall back to the original ID.

Pages own API loading and foreign-key catalogs. The table owns display
formatting, locale handling, and the empty fallback. In development,
`hh-data-table` warns when likely date (`*At`, `*Date`, `*Timestamp`) or
foreign-key (`*Id`) columns have no `format` or `computed` formatter.

Validation messages rendered by `hh-form-renderer` resolve through
`HisHopeValidationMessageRegistry` and the active i18n dictionary.
