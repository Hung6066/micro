import { HisHopeFormFieldSchema } from "@his-hope/frontend-foundation/forms";
import { HisHopeMobileSchemaField } from "@his-hope/frontend-foundation/ui";

/** Maps foundation form schema entries to mobile schema-form fields. */
export function toHisHopeMobileSchemaFields(
  fields: readonly HisHopeFormFieldSchema<unknown>[],
  optionsByKey: Readonly<
    Record<string, readonly { value: string; label: string }[]>
  > = {},
): HisHopeMobileSchemaField[] {
  return fields
    .filter((field) => !field.hidden)
    .map((field) => ({
      key: field.key,
      label: field.label,
      type: mobileFieldType(field),
      placeholder: field.placeholder,
      hint: field.hint,
      options: optionsByKey[field.key] ?? field.options,
      required: field.required,
      hidden: field.hidden,
      multiline: field.type === "textarea",
      rows: field.rows,
      messages: field.messages,
    }));
}

function mobileFieldType(
  field: HisHopeFormFieldSchema<unknown>,
): HisHopeMobileSchemaField["type"] {
  if (field.type === "email") return "email";
  if (field.type === "number") return "number";
  if (field.type === "password") return "password";
  if (field.type === "textarea") return "textarea";
  if (field.options?.length || field.type === "select") return "select";
  return "text";
}
