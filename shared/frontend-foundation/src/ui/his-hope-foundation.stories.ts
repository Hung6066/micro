import type { Meta, StoryObj } from "@storybook/angular";
import { HisHopeStatusBadgeComponent } from "./his-hope-status-badge.component";
import { HisHopeFormFieldComponent } from "./his-hope-form-field.component";
import {
  HisHopeDataTableComponent,
  HisHopeDataTableColumn,
} from "./his-hope-data-table.component";
import { HisHopeCommandPaletteComponent } from "./his-hope-command-palette.component";
import { HisHopeOfflineBannerComponent } from "./his-hope-offline-banner.component";
import { HisHopeButtonComponent } from "./his-hope-button.component";
import { HisHopeAlertComponent } from "./his-hope-alert.component";
import { HisHopeDescriptionListComponent } from "./his-hope-description-list.component";
import { HisHopeTransferListComponent } from "../domain/his-hope-transfer-list.component";
import { HisHopeTimelineComponent } from "../domain/his-hope-timeline.component";
import { HisHopeDiffViewerComponent } from "../domain/his-hope-diff-viewer.component";
import { HisHopeTreeComponent } from "../domain/his-hope-tree.component";
import { HisHopeFormRendererComponent } from "../forms/his-hope-form-renderer.component";
import { createHisHopeFormGroup } from "../forms/his-hope-form-schema";

const meta: Meta = { title: "Foundation/Contracts", tags: ["autodocs"] };
export default meta;
type Story = StoryObj;

export const StatusBadge: Story = {
  render: () => ({
    moduleMetadata: { imports: [HisHopeStatusBadgeComponent] },
    props: { status: "Healthy", label: "Healthy", tone: "success" },
    template: `<hh-status-badge [status]="status" [label]="label" [tone]="tone" />`,
  }),
};

export const FormField: Story = {
  render: () => ({
    moduleMetadata: { imports: [HisHopeFormFieldComponent] },
    template: `<hh-form-field controlId="email" label="Email address" hint="Use your hospital email" required><input id="email" type="email" aria-describedby="email-hint" /></hh-form-field>`,
  }),
};

export const DataTable: Story = {
  render: () => ({
    moduleMetadata: { imports: [HisHopeDataTableComponent] },
    props: {
      columns: [
        { key: "name", label: "Name", sortable: true },
        { key: "status", label: "Status", sortable: true },
      ] satisfies HisHopeDataTableColumn[],
      rows: [
        { id: 1, name: "Identity Service", status: "Healthy" },
        { id: 2, name: "Patient Service", status: "Running" },
      ],
      selection: true,
    },
    template: `<hh-data-table [columns]="columns" [rows]="rows" [selection]="selection" />`,
  }),
};

export const DataTableInlineEdit: Story = {
  render: () => ({
    moduleMetadata: { imports: [HisHopeDataTableComponent] },
    props: {
      columns: [
        {
          key: "name",
          label: "Name",
          editable: true,
          editValidator: (value: unknown) =>
            String(value).length < 2 ? "Enter at least two characters." : null,
        },
        {
          key: "type",
          label: "Type",
          editable: true,
          editor: "select",
          options: [
            { value: "service", label: "Service" },
            { value: "worker", label: "Worker" },
          ],
        },
        { key: "date", label: "Date", editable: true, editor: "date" },
      ] satisfies HisHopeDataTableColumn[],
      rows: [{ id: 1, name: "Identity", type: "service", date: "2026-07-25" }],
      inlineEdit: true,
    },
    template: `<hh-data-table [columns]="columns" [rows]="rows" [inlineEdit]="inlineEdit" />`,
  }),
};

export const ServerTable: Story = {
  render: () => ({
    moduleMetadata: { imports: [HisHopeDataTableComponent] },
    props: {
      columns: [
        { key: "name", label: "Name", sortable: true },
      ] satisfies HisHopeDataTableColumn[],
      rows: [{ id: 1, name: "Page from API" }],
      totalItems: 42,
      mode: "server",
    },
    template: `<hh-data-table [columns]="columns" [rows]="rows" [mode]="mode" [totalItems]="totalItems" />`,
  }),
};

export const DataTableEnterprise: Story = {
  render: () => ({
    moduleMetadata: { imports: [HisHopeDataTableComponent] },
    props: {
      columns: [
        {
          key: "id",
          label: "ID",
          width: "120px",
          reorderable: true,
          hideable: false,
        },
        {
          key: "name",
          label: "Name",
          sortable: true,
          width: "240px",
          reorderable: true,
        },
        { key: "status", label: "Status", sortable: true, reorderable: true },
      ] satisfies HisHopeDataTableColumn[],
      rows: [
        { id: "CL-1042", name: "Identity Service", status: "Healthy" },
        { id: "CL-1043", name: "Clinical Service", status: "Attention" },
      ],
      selection: true,
      exportable: true,
      exportFormats: ["csv", "json"],
      bulkActions: [
        { id: "archive", label: "Archive selected", tone: "danger" },
      ],
      mode: "server",
      totalItems: 128,
      virtualize: true,
      nextCursor: "next-page-token",
      bulkJob: {
        jobId: "bulk-1",
        resource: "services",
        actionId: "archive",
        status: "running",
        processed: 1,
        total: 2,
        rowProgress: [
          { rowKey: "CL-1042", status: "completed" },
          { rowKey: "CL-1043", status: "running" },
        ],
      },
      exportJob: {
        jobId: "export-1",
        resource: "services",
        actionId: "export",
        status: "running",
        processed: 32,
        total: 128,
      },
      query: {
        page: 1,
        pageSize: 20,
        search: "service",
        filterItems: [{ key: "status", operator: "eq", value: "Healthy" }],
      },
    },
    template: `<hh-data-table [columns]="columns" [rows]="rows" [selection]="selection" [exportable]="exportable" [exportFormats]="exportFormats" [bulkActions]="bulkActions" [mode]="mode" [totalItems]="totalItems" [query]="query" />`,
  }),
};

export const CommandPalette: Story = {
  render: () => ({
    moduleMetadata: { imports: [HisHopeCommandPaletteComponent] },
    props: {
      open: true,
      commands: [
        { id: "patients", label: "Open patients", keywords: ["patient"] },
        { id: "settings", label: "Open settings", keywords: ["admin"] },
      ],
    },
    template: `<hh-command-palette [open]="open" [commands]="commands" />`,
  }),
};

export const OfflineState: Story = {
  render: () => ({
    moduleMetadata: { imports: [HisHopeOfflineBannerComponent] },
    template: `<hh-offline-banner />`,
  }),
};

export const Button: Story = {
  render: () => ({
    moduleMetadata: { imports: [HisHopeButtonComponent] },
    template: "<hh-button>Save changes</hh-button>",
  }),
};
export const Alert: Story = {
  render: () => ({
    moduleMetadata: { imports: [HisHopeAlertComponent] },
    template:
      '<hh-alert title="Saved" tone="success">The record is ready.</hh-alert>',
  }),
};
export const DescriptionList: Story = {
  render: () => ({
    moduleMetadata: { imports: [HisHopeDescriptionListComponent] },
    props: {
      items: [
        { term: "Status", description: "Active" },
        { term: "Owner", description: "Admin" },
      ],
    },
    template: '<hh-description-list [items]="items" />',
  }),
};
export const TransferList: Story = {
  render: () => ({
    moduleMetadata: { imports: [HisHopeTransferListComponent] },
    props: {
      items: [
        { id: "read", label: "Read" },
        { id: "write", label: "Write" },
      ],
    },
    template: '<hh-transfer-list [items]="items" />',
  }),
};
export const Timeline: Story = {
  render: () => ({
    moduleMetadata: { imports: [HisHopeTimelineComponent] },
    props: {
      items: [
        { id: "1", title: "Created", date: "Today" },
        { id: "2", title: "Reviewed", detail: "Approved" },
      ],
    },
    template: '<hh-timeline [items]="items" />',
  }),
};
export const DiffViewer: Story = {
  render: () => ({
    moduleMetadata: { imports: [HisHopeDiffViewerComponent] },
    props: {
      lines: [
        { text: "+ granted", kind: "added" },
        { text: "- denied", kind: "removed" },
      ],
    },
    template: '<hh-diff-viewer [lines]="lines" />',
  }),
};
export const Tree: Story = {
  render: () => ({
    moduleMetadata: { imports: [HisHopeTreeComponent] },
    props: {
      nodes: [
        {
          id: "root",
          label: "Permissions",
          children: [{ id: "read", label: "Read" }],
        },
      ],
    },
    template: '<hh-tree [nodes]="nodes" />',
  }),
};
export const FormRenderer: Story = {
  render: () => ({
    moduleMetadata: { imports: [HisHopeFormRendererComponent] },
    props: {
      fields: [
        { key: "name", label: "Name", initialValue: "", required: true },
      ],
      form: createHisHopeFormGroup({
        fields: {
          name: {
            key: "name",
            label: "Name",
            initialValue: "",
            required: true,
          },
        },
      }),
    },
    template: '<hh-form-renderer [fields]="fields" [form]="form" />',
  }),
};
