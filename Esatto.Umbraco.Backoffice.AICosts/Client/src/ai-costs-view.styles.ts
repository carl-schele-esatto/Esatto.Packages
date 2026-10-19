// The Costs tab's styles, from Esatto.Web's ai-costs-view.js (colours validated for light and dark
// backoffice surfaces), plus the budget bar and the previous-period comparison.

import { css } from '@umbraco-cms/backoffice/external/lit';

/** Heatmap cell size (px); the legend swatches use it too. */
export const CELL = 12;

export const styles = css`
    /* The tab is the container the narrow layouts below ask about (@container ai-costs). */
    :host {
        display: block;
        container: ai-costs / inline-size;
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
    /* minmax(0, 1fr): a card is as wide as the tab, not as its widest content (the calendar and the
       tables scroll inside their cards instead). */
    .content {
        display: grid;
        grid-template-columns: minmax(0, 1fr);
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
    /* A figure can't wrap (the currency formatter joins code and amount with a no-break space), so in a
       narrow tile it shrinks to fit: --chars is its length (#figure), at about 0.56em a character in Lato. */
    .tile-value {
        container-type: inline-size;
        font-size: 1.75rem;
        font-weight: 600;
        line-height: 1.2;
        margin: var(--uui-size-space-2) 0;
        color: var(--uui-color-text);
    }
    .tile-value.hero {
        font-size: 3rem;
    }
    .tile-value > span {
        display: block;
        font-size: min(1em, 100cqi / (var(--chars) * 0.56));
        white-space: nowrap;
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
    /* Centred above its bar or cell; --tip-shift (#keepTipInCard) moves it back inside the card at an edge. */
    .tooltip {
        position: absolute;
        transform: translate(calc(-50% + var(--tip-shift, 0px)), calc(-100% - 8px));
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
    .crosshair {
        stroke: var(--viz-axis);
        stroke-width: 1;
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

    /* Narrow tab (phone, narrow window). A card's buttons (#cardActions) move from the header, which can't
       wrap, to the top of the card; "Cost by" shows its rows as a list instead of a 7-column table. */
    .card-actions-body,
    .cost-list {
        display: none;
    }
    @media (max-width: 600px) {
        :host {
            padding: var(--uui-size-space-4);
        }
    }
    @container ai-costs (width < 600px) {
        .card-actions-header {
            display: none;
        }
        .card-actions-body {
            display: flex;
            margin-bottom: var(--uui-size-space-4);
        }
    }
    @container ai-costs (width < 760px) {
        .cost-table {
            display: none;
        }
        .cost-list {
            display: block;
            list-style: none;
            margin: 0;
            padding: 0;
        }
        .cost-list li {
            display: grid;
            grid-template-columns: minmax(0, 1fr) auto;
            gap: var(--uui-size-space-1) var(--uui-size-space-4);
            align-items: baseline;
            padding: var(--uui-size-space-4) 0;
            border-bottom: 1px solid var(--uui-color-divider);
        }
        .cost-list li:last-child {
            border-bottom: 0;
        }
        .cost-list-name {
            font-weight: 600;
            overflow-wrap: anywhere;
        }
        .cost-list-cost {
            text-align: right;
            font-weight: 600;
            font-variant-numeric: tabular-nums;
        }
        .cost-list-tokens {
            color: var(--uui-color-text-alt);
            font-size: var(--uui-type-small-size);
            font-variant-numeric: tabular-nums;
        }
        .cost-list-share {
            font-size: var(--uui-type-small-size);
            font-variant-numeric: tabular-nums;
        }
    }
`;
