import { HisHopeFormFieldSchema } from "@his-hope/frontend-foundation/forms";
import { toHisHopeMobileSchemaFields } from "@his-hope/mobile-foundation/angular";

describe("toMobileSchemaFields", () => {
  it("maps foundation schema entries to mobile schema fields", () => {
    const fields: HisHopeFormFieldSchema<unknown>[] = [
      {
        key: "email",
        label: "Email",
        initialValue: "",
        type: "email",
        required: true,
      },
      {
        key: "role",
        label: "Role",
        initialValue: "admin",
        type: "select",
        options: [{ value: "admin", label: "Admin" }],
      },
      {
        key: "notes",
        label: "Notes",
        initialValue: "",
        type: "textarea",
        hidden: true,
      },
    ];

    expect(toHisHopeMobileSchemaFields(fields)).toEqual([
      jasmine.objectContaining({ key: "email", type: "email" }),
      jasmine.objectContaining({ key: "role", type: "select" }),
    ]);
  });
});
