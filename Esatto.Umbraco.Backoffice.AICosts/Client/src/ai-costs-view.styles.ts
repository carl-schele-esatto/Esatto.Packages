// The Costs tab's styles, from Esatto.Web's ai-costs-view.js (colours validated for light and dark
// backoffice surfaces), plus the budget bar and the previous-period comparison.

import { css } from '@umbraco-cms/backoffice/external/lit';

/** Heatmap cell size (px); the legend swatches use it too. */
export const CELL = 12;

export const styles = css`
    :host {
        display: block;
        padding: var(--uui-size-layout-1);
        --viz-column: #2a78d6;
        --viz-heat-0: #86b6ef;
        --viz-heat-1: #3987e5;
        --viz-heat-2: #1c5cab;
        --viz-heat-3: #0d366b;
        --viz-empty: var(--uui-color-surface-alt);
        --viz-grid: var(--uui-color-divider);
        --viz-axis: var(--uui-color-border-emphasis);
        --viz-muted: var(--uui-color-text-alt);
        /* Categorical slots in fixed order (dataviz reference palette, validated light on #fff and dark on
           Umbraco's #2d333b), and the reserved status colour for failures. */
        --viz-cat-1: #2a78d6;
        --viz-cat-2: #eb6834;
        --viz-cat-3: #1baf7a;
        --viz-cat-4: #eda100;
        --viz-cat-5: #e87ba4;
        --viz-cat-6: #008300;
        --viz-cat-7: #6250d6;
        --viz-cat-8: #e34948;
        --viz-critical: #d03b3b;
        /* "Other" (folded models): neutral, so it never reads as a series hue or a status. */
        --viz-other: #a3a29e;
    }
    :host([dark]) {
        --viz-column: #3987e5;
        --viz-heat-0: #256abf;
        --viz-heat-1: #3987e5;
        --viz-heat-2: #86b6ef;
        --viz-heat-3: #cde2fb;
        --viz-cat-1: #3987e5;
        --viz-cat-2: #d95926;
        --viz-cat-3: #199e70;
        --viz-cat-4: #c98500;
        --viz-cat-5: #d55181;
        --viz-cat-6: #008300;
        --viz-cat-7: #9085e9;
        --viz-cat-8: #e66767;
        --viz-other: #6b6a66;
    }
    .theme-probe {
        position: absolute;
        width: 0;
        height: 0;
        overflow: hidden;
        color: var(--uui-color-surface);
    }
    .filters {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: var(--uui-size-space-5);
        margin-bottom: var(--uui-size-space-5);
    }
    .currency {
        display: inline-flex;
        align-items: center;
        gap: var(--uui-size-space-3);
        color: var(--uui-color-text-alt);
    }
    .currency select {
        font: inherit;
        color: var(--uui-color-text);
        background: var(--uui-color-surface);
        border: 1px solid var(--uui-color-border);
        border-radius: var(--uui-border-radius);
        padding: var(--uui-size-space-2) var(--uui-size-space-3);
    }
    .content {
        display: grid;
        gap: var(--uui-size-space-5);
        transition: opacity 120ms;
    }
    .content.is-loading {
        opacity: 0.5;
    }
    .tiles {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
        gap: var(--uui-size-space-5);
    }
    .tile-label {
        color: var(--uui-color-text-alt);
    }
    .tile-value {
        font-size: 1.75rem;
        font-weight: 600;
        line-height: 1.2;
        margin: var(--uui-size-space-2) 0;
        color: var(--uui-color-text);
    }
    .tile-value.hero {
        font-size: 3rem;
    }
    .tile-note {
        color: var(--uui-color-text-alt);
        font-size: var(--uui-type-small-size);
        display: flex;
        align-items: center;
        gap: var(--uui-size-space-2);
    }
    .card {
        position: relative;
    }
    .heatmap-scroll,
    .table-scroll {
        overflow-x: auto;
    }
    svg {
        display: block;
        font-family: inherit;
    }
    .axis-label {
        fill: var(--viz-muted);
        font-size: 11px;
    }
    .tick {
        text-anchor: end;
        font-variant-numeric: tabular-nums;
    }
    .cell {
        fill: var(--viz-empty);
        cursor: default;
    }
    .cell:hover {
        stroke: var(--uui-color-text);
        stroke-width: 1.5;
    }
    .cell-0 { fill: var(--viz-heat-0); background: var(--viz-heat-0); }
    .cell-1 { fill: var(--viz-heat-1); background: var(--viz-heat-1); }
    .cell-2 { fill: var(--viz-heat-2); background: var(--viz-heat-2); }
    .cell-3 { fill: var(--viz-heat-3); background: var(--viz-heat-3); }
    .cell-empty { fill: var(--viz-empty); background: var(--viz-empty); }
    .scale-legend {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: var(--uui-size-space-2);
        margin-top: var(--uui-size-space-4);
        color: var(--uui-color-text-alt);
        font-size: var(--uui-type-small-size);
    }
    .swatch {
        display: inline-block;
        width: ${CELL}px;
        height: ${CELL}px;
        border-radius: 2px;
    }
    .scale-gap {
        width: var(--uui-size-space-4);
    }
    .scale-range {
        margin-left: auto;
    }
    .column-chart {
        width: 100%;
        overflow: hidden;
    }
    .grid {
        stroke: var(--viz-grid);
        stroke-width: 1;
    }
    .baseline {
        stroke: var(--viz-axis);
        stroke-width: 1;
    }
    .bar {
        fill: var(--viz-column);
        pointer-events: none;
    }
    .hit {
        fill: transparent;
    }
    .hit:hover + .bar {
        opacity: 0.8;
    }
    .tooltip {
        position: absolute;
        transform: translate(-50%, calc(-100% - 8px));
        background: var(--uui-color-surface);
        color: var(--uui-color-text);
        border: 1px solid var(--uui-color-border);
        border-radius: var(--uui-border-radius);
        box-shadow: var(--uui-shadow-depth-3);
        padding: var(--uui-size-space-3) var(--uui-size-space-4);
        pointer-events: none;
        white-space: nowrap;
        z-index: 2;
        font-size: var(--uui-type-small-size);
    }
    .tooltip-title {
        color: var(--uui-color-text-alt);
        margin-bottom: var(--uui-size-space-1);
    }
    .tooltip-line {
        display: flex;
        gap: var(--uui-size-space-2);
        align-items: baseline;
    }
    .tooltip-line strong {
        font-variant-numeric: tabular-nums;
    }
    .tooltip-line span {
        color: var(--uui-color-text-alt);
    }
    .num {
        text-align: right;
        font-variant-numeric: tabular-nums;
    }
    .share {
        display: inline-flex;
        align-items: center;
        gap: var(--uui-size-space-3);
    }
    .share-track {
        display: inline-block;
        width: 60px;
        height: 6px;
        border-radius: 3px;
        background: var(--uui-color-surface-alt);
        overflow: hidden;
    }
    .share-fill {
        display: block;
        height: 100%;
        background: var(--viz-column);
    }
    .empty {
        color: var(--uui-color-text-alt);
        margin: var(--uui-size-space-5) 0;
    }
    .error {
        color: var(--uui-color-danger);
    }
    .footnote {
        color: var(--uui-color-text-alt);
        font-size: var(--uui-type-small-size);
        margin: 0;
    }

    /* This month: budget bar; comparison with the previous period. */
    .tile-change {
        color: var(--uui-color-text-alt);
        font-size: var(--uui-type-small-size);
        margin-top: var(--uui-size-space-1);
    }
    .budget {
        height: 6px;
        border-radius: 3px;
        background: var(--uui-color-surface-alt);
        overflow: hidden;
        margin: var(--uui-size-space-3) 0 var(--uui-size-space-2);
    }
    .budget-fill {
        display: block;
        height: 100%;
        background: var(--viz-column);
    }
    .budget-warning .budget-fill {
        background: var(--uui-color-warning-standalone);
    }
    .budget-over .budget-fill {
        background: var(--uui-color-danger-standalone);
    }
    /* Charts: legend, lines, figures. */
    .legend {
        display: flex;
        flex-wrap: wrap;
        gap: var(--uui-size-space-2) var(--uui-size-space-5);
        margin-bottom: var(--uui-size-space-3);
        color: var(--uui-color-text-alt);
        font-size: var(--uui-type-small-size);
    }
    .legend-item {
        display: inline-flex;
        align-items: center;
        gap: var(--uui-size-space-2);
    }
    .legend-swatch {
        width: 10px;
        height: 10px;
        border-radius: 2px;
    }
    .legend-line {
        width: 18px;
        border-top: 2px solid var(--viz-cat-1);
    }
    .legend-line.dashed {
        border-top-style: dashed;
    }
    .legend-line.budget {
        border-top: 1px solid var(--uui-color-text);
    }
    .line {
        fill: none;
        stroke: var(--viz-cat-1);
        stroke-width: 2;
        stroke-linejoin: round;
        stroke-linecap: round;
        pointer-events: none;
    }
    .line.projection {
        stroke-dasharray: 5 4;
    }
    .dot {
        fill: var(--viz-cat-1);
        stroke: var(--uui-color-surface);
        stroke-width: 2;
        pointer-events: none;
    }
    .budget-line {
        stroke: var(--uui-color-text);
        stroke-width: 1;
    }
    .figures {
        display: flex;
        flex-wrap: wrap;
        gap: var(--uui-size-space-6);
        margin-bottom: var(--uui-size-space-4);
    }
    .figure {
        font-size: 1.5rem;
        font-weight: 600;
        color: var(--uui-color-text);
    }
    .figure-note {
        font-size: var(--uui-type-small-size);
        font-weight: 400;
        color: var(--uui-color-text-alt);
    }
    .chart-note {
        margin: var(--uui-size-space-3) 0 0;
        color: var(--uui-color-text-alt);
        font-size: var(--uui-type-small-size);
    }
    .header-actions {
        display: flex;
        flex-wrap: wrap;
        align-items: center;
        gap: var(--uui-size-space-3);
    }
`;
