import { Component } from "@angular/core";
import { FormsModule } from "@angular/forms";

@Component({ standalone: true, imports: [FormsModule], templateUrl: "./lot-scan-page.component.html", styleUrls: ["./lot-scan-page.component.scss"] })
export class LotScanPageComponent { scannedCode = ""; }
