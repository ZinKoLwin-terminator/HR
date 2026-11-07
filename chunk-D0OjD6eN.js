import{m as o,u as g}from"./chunk-DFeoV5cG.js";import{$ as H3,$r as ss,An as _T,At as Na,Br as nh,Dt as NE,En as Zr,Fn as at$1,Gn as ce,Gr as pN,Gt as RC,H as Ey,Hn as by,Hr as or,Jn as cy,Jr as py,Jt as SN,Kn as ci,Kr as ph,Lr as mv,Mn as _e$1,Mt as Ny,N as Dd,Nt as OC,Or as k,Pt as Oe,R as Ed,Sn as ZD,Tr as je,Tt as NC,U as F,Wn as cN,Wt as R,Xr as qa,Xt as Sg,Yr as qC,Yt as Sd,Z as Gy,_i as wd,_n as Ya,_r as hh,_t as Ke,bn as Z,bt as Ly,c as AN,cn as We,ct as If,di as vs,er as ei,et as HD,f as Ae,fi as vy,g as BC,h as B,ir as fa$1,j as D,k as Cw,ki as zC,lt as Iy,mr as h,nt as Ht,oi as u3,on as Vy,ot as Id,p as Af,pi as w,pr as gy,pt as K,qr as pn,qt as SC,ri as th,rt as Hy,st as Ie,t as $,ti as sy,tn as Td,u as Ab,vi as wy,vn as Yo,w as Cd,wi as yT,wn as Zl,xn as ZC,yn as Yr,yr as hy,z as Ee,zt as Py}from"./chunk-CEgV3Q6Q.js";import{S as wt,a as Pt,c as Z$1,g as lt,h as le,l as Zt$1,n as Dt,s as Vt,t as Ct,w as yi,x as we,y as te}from"./chunk-BN8chEV6.js";import{n as Mt,r as mt,t as Bt}from"./chunk-Bb1hqXYx.js";import{d as tt$1}from"./chunk-DSDxHyPa.js";import{d as U$1,p as fe,t as A$1}from"./chunk-DFKc5as-.js";import{C as It}from"./main-JPN53XDU.js";import{r as Y}from"./chunk-Xylm6EqJ.js";var U=`Method not implemented`;var v=class{locale;_localeChanges=new B;localeChanges=this._localeChanges;setTime(o,e,t,a){throw new Error(U)}getHours(o){throw new Error(U)}getMinutes(o){throw new Error(U)}getSeconds(o){throw new Error(U)}parseTime(o,e){throw new Error(U)}addSeconds(o,e){throw new Error(U)}getValidDateOrNull(o){return this.isDateInstance(o)&&this.isValid(o)?o:null}deserialize(o){return o==null||this.isDateInstance(o)&&this.isValid(o)?o:this.invalid()}setLocale(o){this.locale=o,this._localeChanges.next()}compareDate(o,e){return this.getYear(o)-this.getYear(e)||this.getMonth(o)-this.getMonth(e)||this.getDate(o)-this.getDate(e)}compareTime(o,e){return this.getHours(o)-this.getHours(e)||this.getMinutes(o)-this.getMinutes(e)||this.getSeconds(o)-this.getSeconds(e)}sameDate(o,e){if(o&&e){let t=this.isValid(o),a=this.isValid(e);return t&&a?!this.compareDate(o,e):t==a}return o==e}sameTime(o,e){if(o&&e){let t=this.isValid(o),a=this.isValid(e);return t&&a?!this.compareTime(o,e):t==a}return o==e}clampDate(o,e,t){return e&&this.compareDate(o,e)<0?e:t&&this.compareDate(o,t)>0?t:o}};var W=new D(`mat-date-formats`);var ee=(()=>{class r{changes=new B;calendarLabel=`Calendar`;openCalendarLabel=`Open calendar`;closeCalendarLabel=`Close calendar`;prevMonthLabel=`Previous month`;nextMonthLabel=`Next month`;prevYearLabel=`Previous year`;nextYearLabel=`Next year`;prevMultiYearLabel=`Previous 24 years`;nextMultiYearLabel=`Next 24 years`;switchToMonthViewLabel=`Choose date`;switchToMultiYearViewLabel=`Choose month and year`;startDateLabel=`Start date`;endDateLabel=`End date`;comparisonDateLabel=`Comparison range`;formatYearRange(e,t){return`${e} \u2013 ${t}`}formatYearRangeLabel(e,t){return`${e} to ${t}`}static ɵfac=function(t){return new(t||r)};static ɵprov=F({token:r,factory:r.ɵfac})}return r})();var pa=0;var he=class{value;displayValue;ariaLabel;enabled;compareValue;rawValue;id=pa++;cssClasses;constructor(o,e,t,a,n,i=o,c){this.value=o,this.displayValue=e,this.ariaLabel=t,this.enabled=a,this.compareValue=i,this.rawValue=c,this.cssClasses=n instanceof Set?Array.from(n):n}};var ua={passive:!1,capture:!0};var Te={passive:!0,capture:!0};var Gt={passive:!0};var J=(()=>{class r{_elementRef=h(ce);_ngZone=h($);_platform=h(Oe);_intl=h(ee);_eventCleanups;_skipNextFocus=!1;_focusActiveCellAfterViewChecked=!1;label;rows;todayValue;startValue;endValue;labelMinRequiredCells;numCols=7;activeCell=0;ngAfterViewChecked(){this._focusActiveCellAfterViewChecked&&(this._focusActiveCell(),this._focusActiveCellAfterViewChecked=!1)}isRange=!1;cellAspectRatio=1;comparisonStart=null;comparisonEnd=null;previewStart=null;previewEnd=null;startDateAccessibleName=null;endDateAccessibleName=null;selectedValueChange=new Ee;previewChange=new Ee;activeDateChange=new Ee;dragStarted=new Ee;dragEnded=new Ee;_firstRowOffset;_cellPadding;_cellWidth;_startDateLabelId;_endDateLabelId;_comparisonStartDateLabelId;_comparisonEndDateLabelId;_didDragSinceMouseDown=!1;_injector=h(K);comparisonDateAccessibleName=this._intl.comparisonDateLabel;_trackRow=e=>e;constructor(){let e=h(Yo),t=h(Af);this._startDateLabelId=t.getId(`mat-calendar-body-start-`),this._endDateLabelId=t.getId(`mat-calendar-body-end-`),this._comparisonStartDateLabelId=t.getId(`mat-calendar-body-comparison-start-`),this._comparisonEndDateLabelId=t.getId(`mat-calendar-body-comparison-end-`),h(or).load(H3),this._ngZone.runOutsideAngular(()=>{let a=this._elementRef.nativeElement,n=[e.listen(a,`touchmove`,this._touchmoveHandler,ua),e.listen(a,`mouseenter`,this._enterHandler,Te),e.listen(a,`focus`,this._enterHandler,Te),e.listen(a,`mouseleave`,this._leaveHandler,Te),e.listen(a,`blur`,this._leaveHandler,Te),e.listen(a,`mousedown`,this._mousedownHandler,Gt),e.listen(a,`touchstart`,this._mousedownHandler,Gt)];this._platform.isBrowser&&n.push(e.listen(`window`,`mouseup`,this._mouseupHandler),e.listen(`window`,`touchend`,this._touchendHandler)),this._eventCleanups=n})}_cellClicked(e,t){this._didDragSinceMouseDown||e.enabled&&this.selectedValueChange.emit({value:e.value,event:t})}_emitActiveDateChange(e,t){e.enabled&&this.activeDateChange.emit({value:e.value,event:t})}_isSelected(e){return this.startValue===e||this.endValue===e}ngOnChanges(e){let t=e.numCols,{rows:a,numCols:n}=this;(e.rows||t)&&(this._firstRowOffset=a&&a.length&&a[0].length?n-a[0].length:0),(e.cellAspectRatio||t||!this._cellPadding)&&(this._cellPadding=`${50*this.cellAspectRatio/n}%`),(t||!this._cellWidth)&&(this._cellWidth=`${100/n}%`)}ngOnDestroy(){this._eventCleanups.forEach(e=>e())}_isActiveCell(e,t){let a=e*this.numCols+t;return e&&(a-=this._firstRowOffset),a==this.activeCell}_focusActiveCell(e=!0){Zl(()=>{setTimeout(()=>{let t=this._elementRef.nativeElement.querySelector(`.mat-calendar-body-active`);t&&(e||(this._skipNextFocus=!0),t.focus())})},{injector:this._injector})}_scheduleFocusActiveCellAfterViewChecked(){this._focusActiveCellAfterViewChecked=!0}_isRangeStart(e){return nt(e,this.startValue,this.endValue)}_isRangeEnd(e){return rt(e,this.startValue,this.endValue)}_isInRange(e){return it(e,this.startValue,this.endValue,this.isRange)}_isComparisonStart(e){return nt(e,this.comparisonStart,this.comparisonEnd)}_isComparisonBridgeStart(e,t,a){if(!this._isComparisonStart(e)||this._isRangeStart(e)||!this._isInRange(e))return!1;let n=this.rows[t][a-1];if(!n){let i=this.rows[t-1];n=i&&i[i.length-1]}return n&&!this._isRangeEnd(n.compareValue)}_isComparisonBridgeEnd(e,t,a){if(!this._isComparisonEnd(e)||this._isRangeEnd(e)||!this._isInRange(e))return!1;let n=this.rows[t][a+1];if(!n){let i=this.rows[t+1];n=i&&i[0]}return n&&!this._isRangeStart(n.compareValue)}_isComparisonEnd(e){return rt(e,this.comparisonStart,this.comparisonEnd)}_isInComparisonRange(e){return it(e,this.comparisonStart,this.comparisonEnd,this.isRange)}_isComparisonIdentical(e){return this.comparisonStart===this.comparisonEnd&&e===this.comparisonStart}_isPreviewStart(e){return nt(e,this.previewStart,this.previewEnd)}_isPreviewEnd(e){return rt(e,this.previewStart,this.previewEnd)}_isInPreview(e){return it(e,this.previewStart,this.previewEnd,this.isRange)}_getDescribedby(e){if(!this.isRange)return null;if(this.startValue===e&&this.endValue===e)return`${this._startDateLabelId} ${this._endDateLabelId}`;if(this.startValue===e)return this._startDateLabelId;if(this.endValue===e)return this._endDateLabelId;if(this.comparisonStart!==null&&this.comparisonEnd!==null){if(e===this.comparisonStart&&e===this.comparisonEnd)return`${this._comparisonStartDateLabelId} ${this._comparisonEndDateLabelId}`;if(e===this.comparisonStart)return this._comparisonStartDateLabelId;if(e===this.comparisonEnd)return this._comparisonEndDateLabelId}return null}_enterHandler=e=>{if(this._skipNextFocus&&e.type===`focus`){this._skipNextFocus=!1;return}if(e.target&&this.isRange){let t=this._getCellFromElement(e.target);t&&this._ngZone.run(()=>this.previewChange.emit({value:t.enabled?t:null,event:e}))}};_touchmoveHandler=e=>{if(!this.isRange)return;let t=Ut(e),a=t?this._getCellFromElement(t):null;t!==e.target&&(this._didDragSinceMouseDown=!0),at(e.target)&&e.preventDefault(),this._ngZone.run(()=>this.previewChange.emit({value:a?.enabled?a:null,event:e}))};_leaveHandler=e=>{this.previewEnd!==null&&this.isRange&&(e.type!==`blur`&&(this._didDragSinceMouseDown=!0),e.target&&this._getCellFromElement(e.target)&&!(e.relatedTarget&&this._getCellFromElement(e.relatedTarget))&&this._ngZone.run(()=>this.previewChange.emit({value:null,event:e})))};_mousedownHandler=e=>{if(!this.isRange)return;this._didDragSinceMouseDown=!1;let t=e.target&&this._getCellFromElement(e.target);!t||!this._isInRange(t.compareValue)||this._ngZone.run(()=>{this.dragStarted.emit({value:t.rawValue,event:e})})};_mouseupHandler=e=>{if(!this.isRange)return;let t=at(e.target);if(!t){this._ngZone.run(()=>{this.dragEnded.emit({value:null,event:e})});return}t.closest(`.mat-calendar-body`)===this._elementRef.nativeElement&&this._ngZone.run(()=>{let a=this._getCellFromElement(t);this.dragEnded.emit({value:a?.rawValue??null,event:e})})};_touchendHandler=e=>{let t=Ut(e);t&&this._mouseupHandler({target:t})};_getCellFromElement(e){let t=at(e);if(t){let a=t.getAttribute(`data-mat-row`),n=t.getAttribute(`data-mat-col`);if(a&&n)return this.rows[parseInt(a)]?.[parseInt(n)]||null}return null}static ɵfac=function(t){return new(t||r)};static ɵcmp=(function(){function e(s,p){return this._trackRow(p)}let t=(s,p)=>p.id;function a(s,p){if(s&1&&(Ed(0,`tr`,0)(1,`td`,3),yT(2),Id()()),s&2){let d=zC();Ab(),Ny(`padding-top`,d._cellPadding)(`padding-bottom`,d._cellPadding),ci(`colspan`,d.numCols),Ab(),Sd(` `,d.label,` `)}}function n(s,p){if(s&1&&(Ed(0,`td`,3),yT(1),Id()),s&2){let d=zC(2);Ny(`padding-top`,d._cellPadding)(`padding-bottom`,d._cellPadding),ci(`colspan`,d._firstRowOffset),Ab(),Sd(` `,d._firstRowOffset>=d.labelMinRequiredCells?d.label:``,` `)}}function i(s,p){if(s&1){let d=BC();Ed(0,`td`,6)(1,`button`,7),Iy(`click`,function(I){let _=th(d).$implicit,Pe=zC(2);return nh(Pe._cellClicked(_,I))})(`focus`,function(I){let _=th(d).$implicit,Pe=zC(2);return nh(Pe._emitActiveDateChange(_,I))}),Ed(2,`span`,8),yT(3),Id(),gy(4,`span`,9),Id()()}if(s&2){let d=p.$implicit,m=p.$index,I=zC().$index,_=zC();Ny(`width`,_._cellWidth)(`padding-top`,_._cellPadding)(`padding-bottom`,_._cellPadding),ci(`data-mat-row`,I)(`data-mat-col`,m),Ab(),Td(d.cssClasses),Yr(`mat-calendar-body-disabled`,!d.enabled)(`mat-calendar-body-active`,_._isActiveCell(I,m))(`mat-calendar-body-range-start`,_._isRangeStart(d.compareValue))(`mat-calendar-body-range-end`,_._isRangeEnd(d.compareValue))(`mat-calendar-body-in-range`,_._isInRange(d.compareValue))(`mat-calendar-body-comparison-bridge-start`,_._isComparisonBridgeStart(d.compareValue,I,m))(`mat-calendar-body-comparison-bridge-end`,_._isComparisonBridgeEnd(d.compareValue,I,m))(`mat-calendar-body-comparison-start`,_._isComparisonStart(d.compareValue))(`mat-calendar-body-comparison-end`,_._isComparisonEnd(d.compareValue))(`mat-calendar-body-in-comparison-range`,_._isInComparisonRange(d.compareValue))(`mat-calendar-body-preview-start`,_._isPreviewStart(d.compareValue))(`mat-calendar-body-preview-end`,_._isPreviewEnd(d.compareValue))(`mat-calendar-body-in-preview`,_._isInPreview(d.compareValue)),vy(`tabIndex`,_._isActiveCell(I,m)?0:-1),ci(`aria-label`,d.ariaLabel)(`aria-disabled`,!d.enabled||null)(`aria-pressed`,_._isSelected(d.compareValue))(`aria-current`,_.todayValue===d.compareValue?`date`:null)(`aria-describedby`,_._getDescribedby(d.compareValue)),Ab(),Yr(`mat-calendar-body-selected`,_._isSelected(d.compareValue))(`mat-calendar-body-comparison-identical`,_._isComparisonIdentical(d.compareValue))(`mat-calendar-body-today`,_.todayValue===d.compareValue),Ab(),Sd(` `,d.displayValue,` `)}}function c(s,p){if(s&1&&(Ed(0,`tr`,1),SC(1,n,2,6,`td`,4),RC(2,i,5,49,`td`,5,t),Id()),s&2){let d=p.$implicit,m=p.$index,I=zC();Ab(),NC(m===0&&I._firstRowOffset?1:-1),Ab(),OC(d)}}return Ht({type:r,selectors:[[``,`mat-calendar-body`,``]],hostAttrs:[1,`mat-calendar-body`],inputs:{label:`label`,rows:`rows`,todayValue:`todayValue`,startValue:`startValue`,endValue:`endValue`,labelMinRequiredCells:`labelMinRequiredCells`,numCols:`numCols`,activeCell:`activeCell`,isRange:`isRange`,cellAspectRatio:`cellAspectRatio`,comparisonStart:`comparisonStart`,comparisonEnd:`comparisonEnd`,previewStart:`previewStart`,previewEnd:`previewEnd`,startDateAccessibleName:`startDateAccessibleName`,endDateAccessibleName:`endDateAccessibleName`},outputs:{selectedValueChange:`selectedValueChange`,previewChange:`previewChange`,activeDateChange:`activeDateChange`,dragStarted:`dragStarted`,dragEnded:`dragEnded`},exportAs:[`matCalendarBody`],features:[ei],decls:11,vars:11,consts:[[`aria-hidden`,`true`],[`role`,`row`],[1,`mat-calendar-body-hidden-label`,3,`id`],[1,`mat-calendar-body-label`],[1,`mat-calendar-body-label`,3,`paddingTop`,`paddingBottom`],[`role`,`gridcell`,1,`mat-calendar-body-cell-container`,3,`width`,`paddingTop`,`paddingBottom`],[`role`,`gridcell`,1,`mat-calendar-body-cell-container`],[`type`,`button`,1,`mat-calendar-body-cell`,3,`click`,`focus`,`tabindex`],[1,`mat-calendar-body-cell-content`,`mat-focus-indicator`],[`aria-hidden`,`true`,1,`mat-calendar-body-cell-preview`]],template:function(p,d){p&1&&(SC(0,a,3,6,`tr`,0),RC(1,c,4,1,`tr`,1,e,!0),Ed(3,`span`,2),yT(4),Id(),Ed(5,`span`,2),yT(6),Id(),Ed(7,`span`,2),yT(8),Id(),Ed(9,`span`,2),yT(10),Id()),p&2&&(NC(d._firstRowOffset<d.labelMinRequiredCells?0:-1),Ab(),OC(d.rows),Ab(2),vy(`id`,d._startDateLabelId),Ab(),Sd(` `,d.startDateAccessibleName,`
`),Ab(),vy(`id`,d._endDateLabelId),Ab(),Sd(` `,d.endDateAccessibleName,`
`),Ab(),vy(`id`,d._comparisonStartDateLabelId),Ab(),Py(` `,d.comparisonDateAccessibleName,` `,d.startDateAccessibleName,`
`),Ab(),vy(`id`,d._comparisonEndDateLabelId),Ab(),Py(` `,d.comparisonDateAccessibleName,` `,d.endDateAccessibleName,`
`))},styles:[`.mat-calendar-body {
  min-width: 224px;
}

.mat-calendar-body-today:not(.mat-calendar-body-selected):not(.mat-calendar-body-comparison-identical) {
  border-color: var(--%NS%mat-datepicker-calendar-date-today-outline-color, var(--%NS%mat-sys-primary));
}

.mat-calendar-body-label {
  height: 0;
  line-height: 0;
  text-align: start;
  padding-left: 4.7142857143%;
  padding-right: 4.7142857143%;
  font-size: var(--%NS%mat-datepicker-calendar-body-label-text-size, var(--%NS%mat-sys-title-small-size));
  font-weight: var(--%NS%mat-datepicker-calendar-body-label-text-weight, var(--%NS%mat-sys-title-small-weight));
  color: var(--%NS%mat-datepicker-calendar-body-label-text-color, var(--%NS%mat-sys-on-surface));
}

.mat-calendar-body-hidden-label {
  display: none;
}

.mat-calendar-body-cell-container {
  position: relative;
  height: 0;
  line-height: 0;
}

.mat-calendar-body-cell {
  position: absolute;
  top: 0;
  left: 0;
  width: 100%;
  height: 100%;
  background: none;
  text-align: center;
  outline: none;
  margin: 0;
  font-family: var(--%NS%mat-datepicker-calendar-text-font, var(--%NS%mat-sys-body-medium-font));
  font-size: var(--%NS%mat-datepicker-calendar-text-size, var(--%NS%mat-sys-body-medium-size));
  -webkit-user-select: none;
  user-select: none;
  cursor: pointer;
  outline: none;
  border: none;
  -webkit-tap-highlight-color: transparent;
}
.mat-calendar-body-cell::-moz-focus-inner {
  border: 0;
}

.mat-calendar-body-cell::before,
.mat-calendar-body-cell::after,
.mat-calendar-body-cell-preview {
  content: "";
  position: absolute;
  top: 5%;
  left: 0;
  z-index: 0;
  box-sizing: border-box;
  display: block;
  height: 90%;
  width: 100%;
}

.mat-calendar-body-range-start:not(.mat-calendar-body-in-comparison-range)::before,
.mat-calendar-body-range-start::after,
.mat-calendar-body-comparison-start:not(.mat-calendar-body-comparison-bridge-start)::before,
.mat-calendar-body-comparison-start::after,
.mat-calendar-body-preview-start .mat-calendar-body-cell-preview {
  left: 5%;
  width: 95%;
  border-top-left-radius: 999px;
  border-bottom-left-radius: 999px;
}
[dir=rtl] .mat-calendar-body-range-start:not(.mat-calendar-body-in-comparison-range)::before,
[dir=rtl] .mat-calendar-body-range-start::after,
[dir=rtl] .mat-calendar-body-comparison-start:not(.mat-calendar-body-comparison-bridge-start)::before,
[dir=rtl] .mat-calendar-body-comparison-start::after,
[dir=rtl] .mat-calendar-body-preview-start .mat-calendar-body-cell-preview {
  left: 0;
  border-radius: 0;
  border-top-right-radius: 999px;
  border-bottom-right-radius: 999px;
}

.mat-calendar-body-range-end:not(.mat-calendar-body-in-comparison-range)::before,
.mat-calendar-body-range-end::after,
.mat-calendar-body-comparison-end:not(.mat-calendar-body-comparison-bridge-end)::before,
.mat-calendar-body-comparison-end::after,
.mat-calendar-body-preview-end .mat-calendar-body-cell-preview {
  width: 95%;
  border-top-right-radius: 999px;
  border-bottom-right-radius: 999px;
}
[dir=rtl] .mat-calendar-body-range-end:not(.mat-calendar-body-in-comparison-range)::before,
[dir=rtl] .mat-calendar-body-range-end::after,
[dir=rtl] .mat-calendar-body-comparison-end:not(.mat-calendar-body-comparison-bridge-end)::before,
[dir=rtl] .mat-calendar-body-comparison-end::after,
[dir=rtl] .mat-calendar-body-preview-end .mat-calendar-body-cell-preview {
  left: 5%;
  border-radius: 0;
  border-top-left-radius: 999px;
  border-bottom-left-radius: 999px;
}

[dir=rtl] .mat-calendar-body-comparison-bridge-start.mat-calendar-body-range-end::after,
[dir=rtl] .mat-calendar-body-comparison-bridge-end.mat-calendar-body-range-start::after {
  width: 95%;
  border-top-right-radius: 999px;
  border-bottom-right-radius: 999px;
}

.mat-calendar-body-comparison-start.mat-calendar-body-range-end::after, [dir=rtl] .mat-calendar-body-comparison-start.mat-calendar-body-range-end::after,
.mat-calendar-body-comparison-end.mat-calendar-body-range-start::after,
[dir=rtl] .mat-calendar-body-comparison-end.mat-calendar-body-range-start::after {
  width: 90%;
}

.mat-calendar-body-in-preview {
  color: var(--%NS%mat-datepicker-calendar-date-preview-state-outline-color, var(--%NS%mat-sys-primary));
}
.mat-calendar-body-in-preview .mat-calendar-body-cell-preview {
  border-top: dashed 1px;
  border-bottom: dashed 1px;
}

.mat-calendar-body-preview-start .mat-calendar-body-cell-preview {
  border-left: dashed 1px;
}
[dir=rtl] .mat-calendar-body-preview-start .mat-calendar-body-cell-preview {
  border-left: 0;
  border-right: dashed 1px;
}

.mat-calendar-body-preview-end .mat-calendar-body-cell-preview {
  border-right: dashed 1px;
}
[dir=rtl] .mat-calendar-body-preview-end .mat-calendar-body-cell-preview {
  border-right: 0;
  border-left: dashed 1px;
}

.mat-calendar-body-disabled {
  cursor: default;
}
.mat-calendar-body-disabled > .mat-calendar-body-cell-content:not(.mat-calendar-body-selected):not(.mat-calendar-body-comparison-identical) {
  color: var(--%NS%mat-datepicker-calendar-date-disabled-state-text-color, color-mix(in srgb, var(--%NS%mat-sys-on-surface) 38%, transparent));
}
.mat-calendar-body-disabled > .mat-calendar-body-today:not(.mat-calendar-body-selected):not(.mat-calendar-body-comparison-identical) {
  border-color: var(--%NS%mat-datepicker-calendar-date-today-disabled-state-outline-color, color-mix(in srgb, var(--%NS%mat-sys-on-surface) 38%, transparent));
}
@media (forced-colors: active) {
  .mat-calendar-body-disabled {
    opacity: 0.5;
  }
}

.mat-calendar-body-cell-content {
  top: 5%;
  left: 5%;
  z-index: 1;
  display: flex;
  align-items: center;
  justify-content: center;
  box-sizing: border-box;
  width: 90%;
  height: 90%;
  line-height: 1;
  border-width: 1px;
  border-style: solid;
  border-radius: 999px;
  color: var(--%NS%mat-datepicker-calendar-date-text-color, var(--%NS%mat-sys-on-surface));
  border-color: var(--%NS%mat-datepicker-calendar-date-outline-color, transparent);
}
.mat-calendar-body-cell-content.mat-focus-indicator {
  position: absolute;
}
.mat-calendar-body-cell-content::before {
  border-radius: 50%;
}
@media (forced-colors: active) {
  .mat-calendar-body-cell-content {
    border: none;
  }
}

.cdk-keyboard-focused .mat-calendar-body-active > .mat-calendar-body-cell-content:not(.mat-calendar-body-selected):not(.mat-calendar-body-comparison-identical), .cdk-program-focused .mat-calendar-body-active > .mat-calendar-body-cell-content:not(.mat-calendar-body-selected):not(.mat-calendar-body-comparison-identical) {
  background-color: var(--%NS%mat-datepicker-calendar-date-focus-state-background-color, color-mix(in srgb, var(--%NS%mat-sys-on-surface) calc(var(--%NS%mat-sys-focus-state-layer-opacity) * 100%), transparent));
}

@media (hover: hover) {
  .mat-calendar-body-cell:not(.mat-calendar-body-disabled):hover > .mat-calendar-body-cell-content:not(.mat-calendar-body-selected):not(.mat-calendar-body-comparison-identical) {
    background-color: var(--%NS%mat-datepicker-calendar-date-hover-state-background-color, color-mix(in srgb, var(--%NS%mat-sys-on-surface) calc(var(--%NS%mat-sys-hover-state-layer-opacity) * 100%), transparent));
  }
}
.mat-calendar-body-selected {
  background-color: var(--%NS%mat-datepicker-calendar-date-selected-state-background-color, var(--%NS%mat-sys-primary));
  color: var(--%NS%mat-datepicker-calendar-date-selected-state-text-color, var(--%NS%mat-sys-on-primary));
}
.mat-calendar-body-disabled > .mat-calendar-body-selected {
  background-color: var(--%NS%mat-datepicker-calendar-date-selected-disabled-state-background-color, color-mix(in srgb, var(--%NS%mat-sys-on-surface) 38%, transparent));
}
.mat-calendar-body-selected.mat-calendar-body-today {
  box-shadow: inset 0 0 0 1px var(--%NS%mat-datepicker-calendar-date-today-selected-state-outline-color, var(--%NS%mat-sys-primary));
}

.mat-calendar-body-in-range::before {
  background: var(--%NS%mat-datepicker-calendar-date-in-range-state-background-color, var(--%NS%mat-sys-primary-container));
}

.mat-calendar-body-comparison-identical,
.mat-calendar-body-in-comparison-range::before {
  background: var(--%NS%mat-datepicker-calendar-date-in-comparison-range-state-background-color, var(--%NS%mat-sys-tertiary-container));
}

.mat-calendar-body-comparison-identical,
.mat-calendar-body-in-comparison-range::before {
  background: var(--%NS%mat-datepicker-calendar-date-in-comparison-range-state-background-color, var(--%NS%mat-sys-tertiary-container));
}

.mat-calendar-body-comparison-bridge-start::before,
[dir=rtl] .mat-calendar-body-comparison-bridge-end::before {
  background: linear-gradient(to right, var(--%NS%mat-datepicker-calendar-date-in-range-state-background-color, var(--%NS%mat-sys-primary-container)) 50%, var(--%NS%mat-datepicker-calendar-date-in-comparison-range-state-background-color, var(--%NS%mat-sys-tertiary-container)) 50%);
}

.mat-calendar-body-comparison-bridge-end::before,
[dir=rtl] .mat-calendar-body-comparison-bridge-start::before {
  background: linear-gradient(to left, var(--%NS%mat-datepicker-calendar-date-in-range-state-background-color, var(--%NS%mat-sys-primary-container)) 50%, var(--%NS%mat-datepicker-calendar-date-in-comparison-range-state-background-color, var(--%NS%mat-sys-tertiary-container)) 50%);
}

.mat-calendar-body-in-range > .mat-calendar-body-comparison-identical,
.mat-calendar-body-in-comparison-range.mat-calendar-body-in-range::after {
  background: var(--%NS%mat-datepicker-calendar-date-in-overlap-range-state-background-color, var(--%NS%mat-sys-secondary-container));
}

.mat-calendar-body-comparison-identical.mat-calendar-body-selected,
.mat-calendar-body-in-comparison-range > .mat-calendar-body-selected {
  background: var(--%NS%mat-datepicker-calendar-date-in-overlap-range-selected-state-background-color, var(--%NS%mat-sys-secondary));
}

@media (forced-colors: active) {
  .mat-datepicker-popup:not(:empty),
  .mat-calendar-body-cell:not(.mat-calendar-body-in-range) .mat-calendar-body-selected {
    outline: solid 1px;
  }
  .mat-calendar-body-today {
    outline: dotted 1px;
  }
  .mat-calendar-body-cell::before,
  .mat-calendar-body-cell::after,
  .mat-calendar-body-selected {
    background: none;
  }
  .mat-calendar-body-in-range::before,
  .mat-calendar-body-comparison-bridge-start::before,
  .mat-calendar-body-comparison-bridge-end::before {
    border-top: solid 1px;
    border-bottom: solid 1px;
  }
  .mat-calendar-body-range-start::before {
    border-left: solid 1px;
  }
  [dir=rtl] .mat-calendar-body-range-start::before {
    border-left: 0;
    border-right: solid 1px;
  }
  .mat-calendar-body-range-end::before {
    border-right: solid 1px;
  }
  [dir=rtl] .mat-calendar-body-range-end::before {
    border-right: 0;
    border-left: solid 1px;
  }
  .mat-calendar-body-in-comparison-range::before {
    border-top: dashed 1px;
    border-bottom: dashed 1px;
  }
  .mat-calendar-body-comparison-start::before {
    border-left: dashed 1px;
  }
  [dir=rtl] .mat-calendar-body-comparison-start::before {
    border-left: 0;
    border-right: dashed 1px;
  }
  .mat-calendar-body-comparison-end::before {
    border-right: dashed 1px;
  }
  [dir=rtl] .mat-calendar-body-comparison-end::before {
    border-right: 0;
    border-left: dashed 1px;
  }
}
`],encapsulation:2})})()}return r})();function tt(r){return r?.nodeName===`TD`}function at(r){let o;return tt(r)?o=r:tt(r.parentNode)?o=r.parentNode:tt(r.parentNode?.parentNode)&&(o=r.parentNode.parentNode),o?.getAttribute(`data-mat-row`)!=null?o:null}function nt(r,o,e){return e!==null&&o!==e&&r<e&&r===o}function rt(r,o,e){return o!==null&&o!==e&&r>=o&&r===e}function it(r,o,e,t){return t&&o!==null&&e!==null&&o!==e&&r>=o&&r<=e}function Ut(r){let o=r.changedTouches[0];return document.elementFromPoint(o.clientX,o.clientY)}var x=class{start;end;_disableStructuralEquivalency;constructor(o,e){this.start=o,this.end=e}};var _e=(()=>{class r{selection;_adapter;_selectionChanged=new B;selectionChanged=this._selectionChanged;constructor(e,t){this.selection=e,this._adapter=t,this.selection=e}updateSelection(e,t){let a=this.selection;this.selection=e,this._selectionChanged.next({selection:e,source:t,oldValue:a})}ngOnDestroy(){this._selectionChanged.complete()}_isValidDateInstance(e){return this._adapter.isDateInstance(e)&&this._adapter.isValid(e)}static ɵfac=function(t){Cw()};static ɵprov=R({token:r,factory:r.ɵfac})}return r})();var ma=(()=>{class r extends _e{constructor(e){super(null,e)}add(e){super.updateSelection(e,this)}isValid(){return this.selection!=null&&this._isValidDateInstance(this.selection)}isComplete(){return this.selection!=null}clone(){let e=new r(this._adapter);return e.updateSelection(this.selection,this),e}static ɵfac=function(t){return new(t||r)(w(v))};static ɵprov=R({token:r,factory:r.ɵfac})}return r})();var ta={provide:_e,useFactory:()=>h(_e,{optional:!0,skipSelf:!0})||new ma(h(v))};var aa=new D(`MAT_DATE_RANGE_SELECTION_STRATEGY`);var ot=7;var ha=0;var Qt=(()=>{class r{_changeDetectorRef=h(mv);_dateFormats=h(W,{optional:!0});_dateAdapter=h(v,{optional:!0});_dir=h(cN,{optional:!0});_rangeStrategy=h(aa,{optional:!0});_rerenderSubscription=Z.EMPTY;_selectionKeyPressed=!1;get activeDate(){return this._activeDate}set activeDate(e){let t=this._activeDate,a=this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(e))||this._dateAdapter.today();this._activeDate=this._dateAdapter.clampDate(a,this.minDate,this.maxDate),this._hasSameMonthAndYear(t,this._activeDate)||this._init()}_activeDate;get selected(){return this._selected}set selected(e){e instanceof x?this._selected=e:this._selected=this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(e)),this._setRanges(this._selected)}_selected=null;get minDate(){return this._minDate}set minDate(e){this._minDate=this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(e))}_minDate=null;get maxDate(){return this._maxDate}set maxDate(e){this._maxDate=this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(e))}_maxDate=null;dateFilter;dateClass;comparisonStart=null;comparisonEnd=null;startDateAccessibleName=null;endDateAccessibleName=null;activeDrag=null;selectedChange=new Ee;_userSelection=new Ee;dragStarted=new Ee;dragEnded=new Ee;activeDateChange=new Ee;_matCalendarBody;_monthLabel=_e$1(``);_weeks=_e$1([]);_firstWeekOffset=_e$1(0);_rangeStart=_e$1(null);_rangeEnd=_e$1(null);_comparisonRangeStart=_e$1(null);_comparisonRangeEnd=_e$1(null);_previewStart=_e$1(null);_previewEnd=_e$1(null);_isRange=_e$1(!1);_todayDate=_e$1(null);_weekdays=_e$1([]);constructor(){h(or).load(qa),this._activeDate=this._dateAdapter.today()}ngAfterContentInit(){this._rerenderSubscription=this._dateAdapter.localeChanges.pipe(ss(null)).subscribe(()=>this._init())}ngOnChanges(e){let t=e.comparisonStart||e.comparisonEnd;t&&!t.firstChange&&this._setRanges(this.selected),e.activeDrag&&!this.activeDrag&&this._clearPreview()}ngOnDestroy(){this._rerenderSubscription.unsubscribe()}_dateSelected(e){let t=e.value,a=this._getDateFromDayOfMonth(t),n,i;this._selected instanceof x?(n=this._getDateInCurrentMonth(this._selected.start),i=this._getDateInCurrentMonth(this._selected.end)):n=i=this._getDateInCurrentMonth(this._selected),(n!==t||i!==t)&&this.selectedChange.emit(a),this._userSelection.emit({value:a,event:e.event}),this._clearPreview(),this._changeDetectorRef.markForCheck()}_updateActiveDate(e){let t=e.value,a=this._activeDate;this.activeDate=this._getDateFromDayOfMonth(t),this._dateAdapter.compareDate(a,this.activeDate)&&this.activeDateChange.emit(this._activeDate)}_handleCalendarBodyKeydown(e){let t=this._activeDate,a=this._isRtl();switch(e.keyCode){case 37:this.activeDate=this._dateAdapter.addCalendarDays(this._activeDate,a?1:-1);break;case 39:this.activeDate=this._dateAdapter.addCalendarDays(this._activeDate,a?-1:1);break;case 38:this.activeDate=this._dateAdapter.addCalendarDays(this._activeDate,-7);break;case 40:this.activeDate=this._dateAdapter.addCalendarDays(this._activeDate,7);break;case 36:this.activeDate=this._dateAdapter.addCalendarDays(this._activeDate,1-this._dateAdapter.getDate(this._activeDate));break;case 35:this.activeDate=this._dateAdapter.addCalendarDays(this._activeDate,this._dateAdapter.getNumDaysInMonth(this._activeDate)-this._dateAdapter.getDate(this._activeDate));break;case 33:this.activeDate=e.altKey?this._dateAdapter.addCalendarYears(this._activeDate,-1):this._dateAdapter.addCalendarMonths(this._activeDate,-1);break;case 34:this.activeDate=e.altKey?this._dateAdapter.addCalendarYears(this._activeDate,1):this._dateAdapter.addCalendarMonths(this._activeDate,1);break;case 13:case 32:this._selectionKeyPressed=!0,this._canSelect(this._activeDate)&&e.preventDefault();return;case 27:this._previewEnd()!=null&&!HD(e)&&(this._clearPreview(),this.activeDrag?this.dragEnded.emit({value:null,event:e}):(this.selectedChange.emit(null),this._userSelection.emit({value:null,event:e})),e.preventDefault(),e.stopPropagation());return;default:return}this._dateAdapter.compareDate(t,this.activeDate)&&(this.activeDateChange.emit(this.activeDate),this._focusActiveCellAfterViewChecked()),e.preventDefault()}_handleCalendarBodyKeyup(e){(e.keyCode===32||e.keyCode===13)&&(this._selectionKeyPressed&&this._canSelect(this._activeDate)&&this._dateSelected({value:this._dateAdapter.getDate(this._activeDate),event:e}),this._selectionKeyPressed=!1)}_init(){this._setRanges(this.selected),this._todayDate.set(this._getCellCompareValue(this._dateAdapter.today())),this._monthLabel.set(this._dateFormats.display.monthLabel?this._dateAdapter.format(this.activeDate,this._dateFormats.display.monthLabel):this._dateAdapter.getMonthNames(`short`)[this._dateAdapter.getMonth(this.activeDate)].toLocaleUpperCase());let e=this._dateAdapter.createDate(this._dateAdapter.getYear(this.activeDate),this._dateAdapter.getMonth(this.activeDate),1);this._firstWeekOffset.set((ot+this._dateAdapter.getDayOfWeek(e)-this._dateAdapter.getFirstDayOfWeek())%ot),this._initWeekdays(),this._createWeekCells(),this._changeDetectorRef.markForCheck()}_focusActiveCell(e){this._matCalendarBody._focusActiveCell(e)}_focusActiveCellAfterViewChecked(){this._matCalendarBody._scheduleFocusActiveCellAfterViewChecked()}_previewChanged({event:e,value:t}){if(this._rangeStrategy){let a=t?t.rawValue:null,n=this._rangeStrategy.createPreview(a,this.selected,e);if(this._previewStart.set(this._getCellCompareValue(n.start)),this._previewEnd.set(this._getCellCompareValue(n.end)),this.activeDrag&&a){let i=this._rangeStrategy.createDrag?.(this.activeDrag.value,this.selected,a,e);i&&(this._previewStart.set(this._getCellCompareValue(i.start)),this._previewEnd.set(this._getCellCompareValue(i.end)))}}}_dragEnded(e){if(this.activeDrag)if(e.value){let t=this._rangeStrategy?.createDrag?.(this.activeDrag.value,this.selected,e.value,e.event);this.dragEnded.emit({value:t??null,event:e.event})}else this.dragEnded.emit({value:null,event:e.event})}_getDateFromDayOfMonth(e){return this._dateAdapter.createDate(this._dateAdapter.getYear(this.activeDate),this._dateAdapter.getMonth(this.activeDate),e)}_initWeekdays(){let e=this._dateAdapter.getFirstDayOfWeek(),t=this._dateAdapter.getDayOfWeekNames(`narrow`),n=this._dateAdapter.getDayOfWeekNames(`long`).map((i,c)=>({long:i,narrow:t[c],id:ha++}));this._weekdays.set(n.slice(e).concat(n.slice(0,e)))}_createWeekCells(){let e=this._dateAdapter.getNumDaysInMonth(this.activeDate),t=this._dateAdapter.getDateNames(),a=[[]];for(let n=0,i=this._firstWeekOffset();n<e;n++,i++){i==ot&&(a.push([]),i=0);let c=this._dateAdapter.createDate(this._dateAdapter.getYear(this.activeDate),this._dateAdapter.getMonth(this.activeDate),n+1),s=this._shouldEnableDate(c),p=this._dateAdapter.format(c,this._dateFormats.display.dateA11yLabel),d=this.dateClass?this.dateClass(c,`month`):void 0;a[a.length-1].push(new he(n+1,t[n],p,s,d,this._getCellCompareValue(c),c))}this._weeks.set(a)}_shouldEnableDate(e){return!!e&&(!this.minDate||this._dateAdapter.compareDate(e,this.minDate)>=0)&&(!this.maxDate||this._dateAdapter.compareDate(e,this.maxDate)<=0)&&(!this.dateFilter||this.dateFilter(e))}_getDateInCurrentMonth(e){return e&&this._hasSameMonthAndYear(e,this.activeDate)?this._dateAdapter.getDate(e):null}_hasSameMonthAndYear(e,t){return!!(e&&t&&this._dateAdapter.getMonth(e)==this._dateAdapter.getMonth(t)&&this._dateAdapter.getYear(e)==this._dateAdapter.getYear(t))}_getCellCompareValue(e){if(e){let t=this._dateAdapter.getYear(e),a=this._dateAdapter.getMonth(e),n=this._dateAdapter.getDate(e);return new Date(t,a,n).getTime()}return null}_isRtl(){return this._dir&&this._dir.value===`rtl`}_setRanges(e){e instanceof x?(this._rangeStart.set(this._getCellCompareValue(e.start)),this._rangeEnd.set(this._getCellCompareValue(e.end)),this._isRange.set(!0)):(this._rangeStart.set(this._getCellCompareValue(e)),this._rangeEnd.set(this._rangeStart()),this._isRange.set(!1)),this._comparisonRangeStart.set(this._getCellCompareValue(this.comparisonStart)),this._comparisonRangeEnd.set(this._getCellCompareValue(this.comparisonEnd))}_canSelect(e){return!this.dateFilter||this.dateFilter(e)}_clearPreview(){this._previewStart.set(null),this._previewEnd.set(null)}static ɵfac=function(t){return new(t||r)};static ɵcmp=(function(){let e=(a,n)=>n.id;function t(a,n){if(a&1&&(fa$1(0,`th`,2)(1,`span`,6),yT(2),Dd(),fa$1(3,`span`,3),yT(4),Dd()()),a&2){let i=n.$implicit;Ab(2),Ly(i.long),Ab(2),Ly(i.narrow)}}return Ht({type:r,selectors:[[`mat-month-view`]],viewQuery:function(n,i){if(n&1&&wy(J,5),n&2){let c;qC(c=ZC())&&(i._matCalendarBody=c.first)}},inputs:{activeDate:`activeDate`,selected:`selected`,minDate:`minDate`,maxDate:`maxDate`,dateFilter:`dateFilter`,dateClass:`dateClass`,comparisonStart:`comparisonStart`,comparisonEnd:`comparisonEnd`,startDateAccessibleName:`startDateAccessibleName`,endDateAccessibleName:`endDateAccessibleName`,activeDrag:`activeDrag`},outputs:{selectedChange:`selectedChange`,_userSelection:`_userSelection`,dragStarted:`dragStarted`,dragEnded:`dragEnded`,activeDateChange:`activeDateChange`},exportAs:[`matMonthView`],features:[ei],decls:8,vars:14,consts:[[`role`,`grid`,1,`mat-calendar-table`],[1,`mat-calendar-table-header`],[`scope`,`col`],[`aria-hidden`,`true`],[`colspan`,`7`,1,`mat-calendar-table-header-divider`],[`mat-calendar-body`,``,3,`selectedValueChange`,`activeDateChange`,`previewChange`,`dragStarted`,`dragEnded`,`keyup`,`keydown`,`label`,`rows`,`todayValue`,`startValue`,`endValue`,`comparisonStart`,`comparisonEnd`,`previewStart`,`previewEnd`,`isRange`,`labelMinRequiredCells`,`activeCell`,`startDateAccessibleName`,`endDateAccessibleName`],[1,`cdk-visually-hidden`]],template:function(n,i){n&1&&(fa$1(0,`table`,0)(1,`thead`,1)(2,`tr`),RC(3,t,5,2,`th`,2,e),Dd(),fa$1(5,`tr`,3),hy(6,`th`,4),Dd()(),fa$1(7,`tbody`,5),Ey(`selectedValueChange`,function(s){return i._dateSelected(s)})(`activeDateChange`,function(s){return i._updateActiveDate(s)})(`previewChange`,function(s){return i._previewChanged(s)})(`dragStarted`,function(s){return i.dragStarted.emit(s)})(`dragEnded`,function(s){return i._dragEnded(s)})(`keyup`,function(s){return i._handleCalendarBodyKeyup(s)})(`keydown`,function(s){return i._handleCalendarBodyKeydown(s)}),Dd()()),n&2&&(Ab(3),OC(i._weekdays()),Ab(4),py(`label`,i._monthLabel())(`rows`,i._weeks())(`todayValue`,i._todayDate())(`startValue`,i._rangeStart())(`endValue`,i._rangeEnd())(`comparisonStart`,i._comparisonRangeStart())(`comparisonEnd`,i._comparisonRangeEnd())(`previewStart`,i._previewStart())(`previewEnd`,i._previewEnd())(`isRange`,i._isRange())(`labelMinRequiredCells`,3)(`activeCell`,i._dateAdapter.getDate(i.activeDate)-1)(`startDateAccessibleName`,i.startDateAccessibleName)(`endDateAccessibleName`,i.endDateAccessibleName))},dependencies:[J],encapsulation:2})})()}return r})();var A=24;var st=4;var Zt=(()=>{class r{_changeDetectorRef=h(mv);_dateAdapter=h(v,{optional:!0});_dir=h(cN,{optional:!0});_rerenderSubscription=Z.EMPTY;_selectionKeyPressed=!1;get activeDate(){return this._activeDate}set activeDate(e){let t=this._activeDate,a=this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(e))||this._dateAdapter.today();this._activeDate=this._dateAdapter.clampDate(a,this.minDate,this.maxDate),na(this._dateAdapter,t,this._activeDate,this.minDate,this.maxDate)||this._init()}_activeDate;get selected(){return this._selected}set selected(e){e instanceof x?this._selected=e:this._selected=this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(e)),this._setSelectedYear(e)}_selected=null;get minDate(){return this._minDate}set minDate(e){this._minDate=this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(e))}_minDate=null;get maxDate(){return this._maxDate}set maxDate(e){this._maxDate=this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(e))}_maxDate=null;dateFilter;dateClass;selectedChange=new Ee;yearSelected=new Ee;activeDateChange=new Ee;_matCalendarBody;_years=_e$1([]);_todayYear=_e$1(0);_selectedYear=_e$1(null);constructor(){this._dateAdapter,this._activeDate=this._dateAdapter.today()}ngAfterContentInit(){this._rerenderSubscription=this._dateAdapter.localeChanges.pipe(ss(null)).subscribe(()=>this._init())}ngOnDestroy(){this._rerenderSubscription.unsubscribe()}_init(){this._todayYear.set(this._dateAdapter.getYear(this._dateAdapter.today()));let t=this._dateAdapter.getYear(this._activeDate)-ue(this._dateAdapter,this.activeDate,this.minDate,this.maxDate),a=[];for(let n=0,i=[];n<A;n++)i.push(t+n),i.length==st&&(a.push(i.map(c=>this._createCellForYear(c))),i=[]);this._years.set(a),this._changeDetectorRef.markForCheck()}_yearSelected(e){let t=e.value,a=this._dateAdapter.createDate(t,0,1),n=this._getDateFromYear(t);this.yearSelected.emit(a),this.selectedChange.emit(n)}_updateActiveDate(e){let t=e.value,a=this._activeDate;this.activeDate=this._getDateFromYear(t),this._dateAdapter.compareDate(a,this.activeDate)&&this.activeDateChange.emit(this.activeDate)}_handleCalendarBodyKeydown(e){let t=this._activeDate,a=this._isRtl();switch(e.keyCode){case 37:this.activeDate=this._dateAdapter.addCalendarYears(this._activeDate,a?1:-1);break;case 39:this.activeDate=this._dateAdapter.addCalendarYears(this._activeDate,a?-1:1);break;case 38:this.activeDate=this._dateAdapter.addCalendarYears(this._activeDate,-st);break;case 40:this.activeDate=this._dateAdapter.addCalendarYears(this._activeDate,st);break;case 36:this.activeDate=this._dateAdapter.addCalendarYears(this._activeDate,-ue(this._dateAdapter,this.activeDate,this.minDate,this.maxDate));break;case 35:this.activeDate=this._dateAdapter.addCalendarYears(this._activeDate,A-ue(this._dateAdapter,this.activeDate,this.minDate,this.maxDate)-1);break;case 33:this.activeDate=this._dateAdapter.addCalendarYears(this._activeDate,e.altKey?-A*10:-A);break;case 34:this.activeDate=this._dateAdapter.addCalendarYears(this._activeDate,e.altKey?A*10:A);break;case 13:case 32:this._selectionKeyPressed=!0;break;default:return}this._dateAdapter.compareDate(t,this.activeDate)&&this.activeDateChange.emit(this.activeDate),this._focusActiveCellAfterViewChecked(),e.preventDefault()}_handleCalendarBodyKeyup(e){(e.keyCode===32||e.keyCode===13)&&(this._selectionKeyPressed&&this._yearSelected({value:this._dateAdapter.getYear(this._activeDate),event:e}),this._selectionKeyPressed=!1)}_getActiveCell(){return ue(this._dateAdapter,this.activeDate,this.minDate,this.maxDate)}_focusActiveCell(){this._matCalendarBody._focusActiveCell()}_focusActiveCellAfterViewChecked(){this._matCalendarBody._scheduleFocusActiveCellAfterViewChecked()}_getDateFromYear(e){let t=this._dateAdapter.getMonth(this.activeDate),a=this._dateAdapter.getNumDaysInMonth(this._dateAdapter.createDate(e,t,1));return this._dateAdapter.createDate(e,t,Math.min(this._dateAdapter.getDate(this.activeDate),a))}_createCellForYear(e){let t=this._dateAdapter.createDate(e,0,1),a=this._dateAdapter.getYearName(t),n=this.dateClass?this.dateClass(t,`multi-year`):void 0;return new he(e,a,a,this._shouldEnableYear(e),n)}_shouldEnableYear(e){if(e==null||this.maxDate&&e>this._dateAdapter.getYear(this.maxDate)||this.minDate&&e<this._dateAdapter.getYear(this.minDate))return!1;if(!this.dateFilter)return!0;let t=this._dateAdapter.createDate(e,0,1);for(let a=t;this._dateAdapter.getYear(a)==e;a=this._dateAdapter.addCalendarDays(a,1))if(this.dateFilter(a))return!0;return!1}_isRtl(){return this._dir&&this._dir.value===`rtl`}_setSelectedYear(e){if(this._selectedYear.set(null),e instanceof x){let t=e.start||e.end;t&&this._selectedYear.set(this._dateAdapter.getYear(t))}else e&&this._selectedYear.set(this._dateAdapter.getYear(e))}static ɵfac=function(t){return new(t||r)};static ɵcmp=Ht({type:r,selectors:[[`mat-multi-year-view`]],viewQuery:function(t,a){if(t&1&&wy(J,5),t&2){let n;qC(n=ZC())&&(a._matCalendarBody=n.first)}},inputs:{activeDate:`activeDate`,selected:`selected`,minDate:`minDate`,maxDate:`maxDate`,dateFilter:`dateFilter`,dateClass:`dateClass`},outputs:{selectedChange:`selectedChange`,yearSelected:`yearSelected`,activeDateChange:`activeDateChange`},exportAs:[`matMultiYearView`],decls:5,vars:7,consts:[[`role`,`grid`,1,`mat-calendar-table`],[`aria-hidden`,`true`,1,`mat-calendar-table-header`],[`colspan`,`4`,1,`mat-calendar-table-header-divider`],[`mat-calendar-body`,``,3,`selectedValueChange`,`activeDateChange`,`keyup`,`keydown`,`rows`,`todayValue`,`startValue`,`endValue`,`numCols`,`cellAspectRatio`,`activeCell`]],template:function(t,a){t&1&&(fa$1(0,`table`,0)(1,`thead`,1)(2,`tr`),hy(3,`th`,2),Dd()(),fa$1(4,`tbody`,3),Ey(`selectedValueChange`,function(i){return a._yearSelected(i)})(`activeDateChange`,function(i){return a._updateActiveDate(i)})(`keyup`,function(i){return a._handleCalendarBodyKeyup(i)})(`keydown`,function(i){return a._handleCalendarBodyKeydown(i)}),Dd()()),t&2&&(Ab(4),py(`rows`,a._years())(`todayValue`,a._todayYear())(`startValue`,a._selectedYear())(`endValue`,a._selectedYear())(`numCols`,4)(`cellAspectRatio`,4/7)(`activeCell`,a._getActiveCell()))},dependencies:[J],encapsulation:2})}return r})();function na(r,o,e,t,a){let n=r.getYear(o),i=r.getYear(e),c=ra(r,t,a);return Math.floor((n-c)/A)===Math.floor((i-c)/A)}function ue(r,o,e,t){return _a(r.getYear(o)-ra(r,e,t),A)}function ra(r,o,e){let t=0;return e?t=r.getYear(e)-A+1:o&&(t=r.getYear(o)),t}function _a(r,o){return(r%o+o)%o}var Xt=(()=>{class r{_changeDetectorRef=h(mv);_dateFormats=h(W,{optional:!0});_dateAdapter=h(v,{optional:!0});_dir=h(cN,{optional:!0});_rerenderSubscription=Z.EMPTY;_selectionKeyPressed=!1;get activeDate(){return this._activeDate}set activeDate(e){let t=this._activeDate,a=this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(e))||this._dateAdapter.today();this._activeDate=this._dateAdapter.clampDate(a,this.minDate,this.maxDate),this._dateAdapter.getYear(t)!==this._dateAdapter.getYear(this._activeDate)&&this._init()}_activeDate;get selected(){return this._selected}set selected(e){e instanceof x?this._selected=e:this._selected=this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(e)),this._setSelectedMonth(e)}_selected=null;get minDate(){return this._minDate}set minDate(e){this._minDate=this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(e))}_minDate=null;get maxDate(){return this._maxDate}set maxDate(e){this._maxDate=this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(e))}_maxDate=null;dateFilter;dateClass;selectedChange=new Ee;monthSelected=new Ee;activeDateChange=new Ee;_matCalendarBody;_months=_e$1([]);_yearLabel=_e$1(``);_todayMonth=_e$1(null);_selectedMonth=_e$1(null);constructor(){this._activeDate=this._dateAdapter.today()}ngAfterContentInit(){this._rerenderSubscription=this._dateAdapter.localeChanges.pipe(ss(null)).subscribe(()=>this._init())}ngOnDestroy(){this._rerenderSubscription.unsubscribe()}_monthSelected(e){let t=e.value,a=this._dateAdapter.createDate(this._dateAdapter.getYear(this.activeDate),t,1);this.monthSelected.emit(a);let n=this._getDateFromMonth(t);this.selectedChange.emit(n)}_updateActiveDate(e){let t=e.value,a=this._activeDate;this.activeDate=this._getDateFromMonth(t),this._dateAdapter.compareDate(a,this.activeDate)&&this.activeDateChange.emit(this.activeDate)}_handleCalendarBodyKeydown(e){let t=this._activeDate,a=this._isRtl();switch(e.keyCode){case 37:this.activeDate=this._dateAdapter.addCalendarMonths(this._activeDate,a?1:-1);break;case 39:this.activeDate=this._dateAdapter.addCalendarMonths(this._activeDate,a?-1:1);break;case 38:this.activeDate=this._dateAdapter.addCalendarMonths(this._activeDate,-4);break;case 40:this.activeDate=this._dateAdapter.addCalendarMonths(this._activeDate,4);break;case 36:this.activeDate=this._dateAdapter.addCalendarMonths(this._activeDate,-this._dateAdapter.getMonth(this._activeDate));break;case 35:this.activeDate=this._dateAdapter.addCalendarMonths(this._activeDate,11-this._dateAdapter.getMonth(this._activeDate));break;case 33:this.activeDate=this._dateAdapter.addCalendarYears(this._activeDate,e.altKey?-10:-1);break;case 34:this.activeDate=this._dateAdapter.addCalendarYears(this._activeDate,e.altKey?10:1);break;case 13:case 32:this._selectionKeyPressed=!0;break;default:return}this._dateAdapter.compareDate(t,this.activeDate)&&(this.activeDateChange.emit(this.activeDate),this._focusActiveCellAfterViewChecked()),e.preventDefault()}_handleCalendarBodyKeyup(e){(e.keyCode===32||e.keyCode===13)&&(this._selectionKeyPressed&&this._monthSelected({value:this._dateAdapter.getMonth(this._activeDate),event:e}),this._selectionKeyPressed=!1)}_init(){this._setSelectedMonth(this.selected),this._todayMonth.set(this._getMonthInCurrentYear(this._dateAdapter.today())),this._yearLabel.set(this._dateAdapter.getYearName(this.activeDate));let e=this._dateAdapter.getMonthNames(`short`);this._months.set([[0,1,2,3],[4,5,6,7],[8,9,10,11]].map(t=>t.map(a=>this._createCellForMonth(a,e[a])))),this._changeDetectorRef.markForCheck()}_focusActiveCell(){this._matCalendarBody._focusActiveCell()}_focusActiveCellAfterViewChecked(){this._matCalendarBody._scheduleFocusActiveCellAfterViewChecked()}_getMonthInCurrentYear(e){return e&&this._dateAdapter.getYear(e)==this._dateAdapter.getYear(this.activeDate)?this._dateAdapter.getMonth(e):null}_getDateFromMonth(e){let t=this._dateAdapter.createDate(this._dateAdapter.getYear(this.activeDate),e,1),a=this._dateAdapter.getNumDaysInMonth(t);return this._dateAdapter.createDate(this._dateAdapter.getYear(this.activeDate),e,Math.min(this._dateAdapter.getDate(this.activeDate),a))}_createCellForMonth(e,t){let a=this._dateAdapter.createDate(this._dateAdapter.getYear(this.activeDate),e,1),n=this._dateAdapter.format(a,this._dateFormats.display.monthYearA11yLabel),i=this.dateClass?this.dateClass(a,`year`):void 0;return new he(e,t.toLocaleUpperCase(),n,this._shouldEnableMonth(e),i)}_shouldEnableMonth(e){let t=this._dateAdapter.getYear(this.activeDate);if(e==null||this._isYearAndMonthAfterMaxDate(t,e)||this._isYearAndMonthBeforeMinDate(t,e))return!1;if(!this.dateFilter)return!0;let a=this._dateAdapter.createDate(t,e,1);for(let n=a;this._dateAdapter.getMonth(n)==e;n=this._dateAdapter.addCalendarDays(n,1))if(this.dateFilter(n))return!0;return!1}_isYearAndMonthAfterMaxDate(e,t){if(this.maxDate){let a=this._dateAdapter.getYear(this.maxDate),n=this._dateAdapter.getMonth(this.maxDate);return e>a||e===a&&t>n}return!1}_isYearAndMonthBeforeMinDate(e,t){if(this.minDate){let a=this._dateAdapter.getYear(this.minDate),n=this._dateAdapter.getMonth(this.minDate);return e<a||e===a&&t<n}return!1}_isRtl(){return this._dir&&this._dir.value===`rtl`}_setSelectedMonth(e){e instanceof x?this._selectedMonth.set(this._getMonthInCurrentYear(e.start)||this._getMonthInCurrentYear(e.end)):this._selectedMonth.set(this._getMonthInCurrentYear(e))}static ɵfac=function(t){return new(t||r)};static ɵcmp=Ht({type:r,selectors:[[`mat-year-view`]],viewQuery:function(t,a){if(t&1&&wy(J,5),t&2){let n;qC(n=ZC())&&(a._matCalendarBody=n.first)}},inputs:{activeDate:`activeDate`,selected:`selected`,minDate:`minDate`,maxDate:`maxDate`,dateFilter:`dateFilter`,dateClass:`dateClass`},outputs:{selectedChange:`selectedChange`,monthSelected:`monthSelected`,activeDateChange:`activeDateChange`},exportAs:[`matYearView`],decls:5,vars:9,consts:[[`role`,`grid`,1,`mat-calendar-table`],[`aria-hidden`,`true`,1,`mat-calendar-table-header`],[`colspan`,`4`,1,`mat-calendar-table-header-divider`],[`mat-calendar-body`,``,3,`selectedValueChange`,`activeDateChange`,`keyup`,`keydown`,`label`,`rows`,`todayValue`,`startValue`,`endValue`,`labelMinRequiredCells`,`numCols`,`cellAspectRatio`,`activeCell`]],template:function(t,a){t&1&&(fa$1(0,`table`,0)(1,`thead`,1)(2,`tr`),hy(3,`th`,2),Dd()(),fa$1(4,`tbody`,3),Ey(`selectedValueChange`,function(i){return a._monthSelected(i)})(`activeDateChange`,function(i){return a._updateActiveDate(i)})(`keyup`,function(i){return a._handleCalendarBodyKeyup(i)})(`keydown`,function(i){return a._handleCalendarBodyKeydown(i)}),Dd()()),t&2&&(Ab(4),py(`label`,a._yearLabel())(`rows`,a._months())(`todayValue`,a._todayMonth())(`startValue`,a._selectedMonth())(`endValue`,a._selectedMonth())(`labelMinRequiredCells`,2)(`numCols`,4)(`cellAspectRatio`,4/7)(`activeCell`,a._dateAdapter.getMonth(a.activeDate)))},dependencies:[J],encapsulation:2})}return r})();var ia=(()=>{class r{_intl=h(ee);calendar=h(dt);_dateAdapter=h(v,{optional:!0});_dateFormats=h(W,{optional:!0});_periodButtonText;_periodButtonDescription;_periodButtonLabel;_prevButtonLabel;_nextButtonLabel;constructor(){h(or).load(qa);let e=h(mv);this._updateLabels(),this.calendar.stateChanges.subscribe(()=>{this._updateLabels(),e.markForCheck()})}get periodButtonText(){return this._periodButtonText}get periodButtonDescription(){return this._periodButtonDescription}get periodButtonLabel(){return this._periodButtonLabel}get prevButtonLabel(){return this._prevButtonLabel}get nextButtonLabel(){return this._nextButtonLabel}currentPeriodClicked(){this.calendar.currentView=this.calendar.currentView==`month`?`multi-year`:`month`}previousClicked(){this.previousEnabled()&&(this.calendar.activeDate=this.calendar.currentView==`month`?this._dateAdapter.addCalendarMonths(this.calendar.activeDate,-1):this._dateAdapter.addCalendarYears(this.calendar.activeDate,this.calendar.currentView==`year`?-1:-A))}nextClicked(){this.nextEnabled()&&(this.calendar.activeDate=this.calendar.currentView==`month`?this._dateAdapter.addCalendarMonths(this.calendar.activeDate,1):this._dateAdapter.addCalendarYears(this.calendar.activeDate,this.calendar.currentView==`year`?1:A))}previousEnabled(){return this.calendar.minDate?!this.calendar.minDate||!this._isSameView(this.calendar.activeDate,this.calendar.minDate):!0}nextEnabled(){return!this.calendar.maxDate||!this._isSameView(this.calendar.activeDate,this.calendar.maxDate)}_updateLabels(){let e=this.calendar,t=this._intl,a=this._dateAdapter;e.currentView===`month`?(this._periodButtonText=a.format(e.activeDate,this._dateFormats.display.monthYearLabel).toLocaleUpperCase(),this._periodButtonDescription=a.format(e.activeDate,this._dateFormats.display.monthYearLabel).toLocaleUpperCase(),this._periodButtonLabel=t.switchToMultiYearViewLabel,this._prevButtonLabel=t.prevMonthLabel,this._nextButtonLabel=t.nextMonthLabel):e.currentView===`year`?(this._periodButtonText=a.getYearName(e.activeDate),this._periodButtonDescription=a.getYearName(e.activeDate),this._periodButtonLabel=t.switchToMonthViewLabel,this._prevButtonLabel=t.prevYearLabel,this._nextButtonLabel=t.nextYearLabel):(this._periodButtonText=t.formatYearRange(...this._formatMinAndMaxYearLabels()),this._periodButtonDescription=t.formatYearRangeLabel(...this._formatMinAndMaxYearLabels()),this._periodButtonLabel=t.switchToMonthViewLabel,this._prevButtonLabel=t.prevMultiYearLabel,this._nextButtonLabel=t.nextMultiYearLabel)}_isSameView(e,t){return this.calendar.currentView==`month`?this._dateAdapter.getYear(e)==this._dateAdapter.getYear(t)&&this._dateAdapter.getMonth(e)==this._dateAdapter.getMonth(t):this.calendar.currentView==`year`?this._dateAdapter.getYear(e)==this._dateAdapter.getYear(t):na(this._dateAdapter,e,t,this.calendar.minDate,this.calendar.maxDate)}_formatMinAndMaxYearLabels(){let t=this._dateAdapter.getYear(this.calendar.activeDate)-ue(this._dateAdapter,this.calendar.activeDate,this.calendar.minDate,this.calendar.maxDate),a=t+A-1;return[this._dateAdapter.getYearName(this._dateAdapter.createDate(t,0,1)),this._dateAdapter.getYearName(this._dateAdapter.createDate(a,0,1))]}_periodButtonLabelId=h(Af).getId(`mat-calendar-period-label-`);static ɵfac=function(t){return new(t||r)};static ɵcmp=(function(){return Ht({type:r,selectors:[[`mat-calendar-header`]],exportAs:[`matCalendarHeader`],ngContentSelectors:[`*`],decls:17,vars:13,consts:[[1,`mat-calendar-header`],[1,`mat-calendar-controls`],[`aria-live`,`polite`,1,`cdk-visually-hidden`,3,`id`],[`matButton`,``,`type`,`button`,1,`mat-calendar-period-button`,3,`click`],[`aria-hidden`,`true`],[`viewBox`,`0 0 10 5`,`focusable`,`false`,`aria-hidden`,`true`,1,`mat-calendar-arrow`],[`points`,`0,0 5,5 10,0`],[1,`mat-calendar-spacer`],[`matIconButton`,``,`type`,`button`,`disabledInteractive`,``,1,`mat-calendar-previous-button`,3,`click`,`disabled`,`matTooltip`],[`viewBox`,`0 0 24 24`,`focusable`,`false`,`aria-hidden`,`true`],[`d`,`M15.41 7.41L14 6l-6 6 6 6 1.41-1.41L10.83 12z`],[`matIconButton`,``,`type`,`button`,`disabledInteractive`,``,1,`mat-calendar-next-button`,3,`click`,`disabled`,`matTooltip`],[`d`,`M10 6L8.59 7.41 13.17 12l-4.58 4.59L10 18l6-6z`]],template:function(a,n){a&1&&(wd(),fa$1(0,`div`,0)(1,`div`,1)(2,`span`,2),yT(3),Dd(),fa$1(4,`button`,3),Ey(`click`,function(){return n.currentPeriodClicked()}),fa$1(5,`span`,4),yT(6),Dd(),ph(),fa$1(7,`svg`,5),hy(8,`polygon`,6),Dd()(),hh(),hy(9,`div`,7),Cd(10),fa$1(11,`button`,8),Ey(`click`,function(){return n.previousClicked()}),ph(),fa$1(12,`svg`,9),hy(13,`path`,10),Dd()(),hh(),fa$1(14,`button`,11),Ey(`click`,function(){return n.nextClicked()}),ph(),fa$1(15,`svg`,9),hy(16,`path`,12),Dd()()()()),a&2&&(Ab(2),py(`id`,n._periodButtonLabelId),Ab(),Ly(n.periodButtonDescription),Ab(),ci(`aria-label`,n.periodButtonLabel)(`aria-describedby`,n._periodButtonLabelId),Ab(2),Ly(n.periodButtonText),Ab(),Yr(`mat-calendar-invert`,n.calendar.currentView!==`month`),Ab(4),py(`disabled`,!n.previousEnabled())(`matTooltip`,n.prevButtonLabel),ci(`aria-label`,n.prevButtonLabel),Ab(3),py(`disabled`,!n.nextEnabled())(`matTooltip`,n.nextButtonLabel),ci(`aria-label`,n.nextButtonLabel))},dependencies:[Mt,mt,It],encapsulation:2})})()}return r})();var dt=(()=>{class r{_dateAdapter=h(v,{optional:!0});_dateFormats=h(W,{optional:!0});_changeDetectorRef=h(mv);_elementRef=h(ce);headerComponent;_calendarHeaderPortal;_intlChanges;_moveFocusOnNextTick=!1;get startAt(){return this._startAt}set startAt(e){this._startAt=this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(e))}_startAt=null;startView=`month`;get selected(){return this._selected}set selected(e){e instanceof x?this._selected=e:this._selected=this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(e))}_selected=null;get minDate(){return this._minDate}set minDate(e){this._minDate=this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(e))}_minDate=null;get maxDate(){return this._maxDate}set maxDate(e){this._maxDate=this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(e))}_maxDate=null;dateFilter;dateClass;comparisonStart=null;comparisonEnd=null;startDateAccessibleName=null;endDateAccessibleName=null;selectedChange=new Ee;yearSelected=new Ee;monthSelected=new Ee;viewChanged=new Ee(!0);_userSelection=new Ee;_userDragDrop=new Ee;monthView;yearView;multiYearView;get activeDate(){return this._clampedActiveDate}set activeDate(e){this._clampedActiveDate=this._dateAdapter.clampDate(e,this.minDate,this.maxDate),this.stateChanges.next(),this._changeDetectorRef.markForCheck()}_clampedActiveDate;get currentView(){return this._currentView}set currentView(e){let t=this._currentView!==e?e:null;this._currentView=e,this._moveFocusOnNextTick=!0,this._changeDetectorRef.markForCheck(),t&&(this.stateChanges.next(),this.viewChanged.emit(t))}_currentView;_activeDrag=null;stateChanges=new B;constructor(){this._intlChanges=h(ee).changes.subscribe(()=>{this._changeDetectorRef.markForCheck(),this.stateChanges.next()})}ngAfterContentInit(){this._calendarHeaderPortal=new Ct(this.headerComponent||ia),this.activeDate=this.startAt||this._dateAdapter.today(),this._currentView=this.startView}ngAfterViewChecked(){this._moveFocusOnNextTick&&(this._moveFocusOnNextTick=!1,this.focusActiveCell())}ngOnDestroy(){this._intlChanges.unsubscribe(),this.stateChanges.complete()}ngOnChanges(e){let t=e.minDate&&!this._dateAdapter.sameDate(e.minDate.previousValue,e.minDate.currentValue)?e.minDate:void 0,a=e.maxDate&&!this._dateAdapter.sameDate(e.maxDate.previousValue,e.maxDate.currentValue)?e.maxDate:void 0,n=t||a||e.dateFilter;if(n&&!n.firstChange){let i=this._getCurrentViewComponent();i&&(this._elementRef.nativeElement.contains(If())&&(this._moveFocusOnNextTick=!0),this._changeDetectorRef.detectChanges(),i._init())}this.stateChanges.next()}focusActiveCell(){this._getCurrentViewComponent()?._focusActiveCell(!1)}updateTodaysDate(){this._getCurrentViewComponent()?._init()}_dateSelected(e){let t=e.value;(this.selected instanceof x||t&&!this._dateAdapter.sameDate(t,this.selected))&&this.selectedChange.emit(t),this._userSelection.emit(e)}_yearSelectedInMultiYearView(e){this.yearSelected.emit(e)}_monthSelectedInYearView(e){this.monthSelected.emit(e)}_goToDateInView(e,t){this.activeDate=e,this.currentView=t}_dragStarted(e){this._activeDrag=e}_dragEnded(e){this._activeDrag&&(e.value&&this._userDragDrop.emit(e),this._activeDrag=null)}_getCurrentViewComponent(){return this.monthView||this.yearView||this.multiYearView}static ɵfac=function(t){return new(t||r)};static ɵcmp=(function(){function e(i,c){}function t(i,c){if(i&1){let s=BC();fa$1(0,`mat-month-view`,4),Hy(`activeDateChange`,function(d){th(s);let m=zC();return _T(m.activeDate,d)||(m.activeDate=d),nh(d)}),Ey(`_userSelection`,function(d){th(s);let m=zC();return nh(m._dateSelected(d))})(`dragStarted`,function(d){th(s);let m=zC();return nh(m._dragStarted(d))})(`dragEnded`,function(d){th(s);let m=zC();return nh(m._dragEnded(d))}),Dd()}if(i&2){let s=zC();Vy(`activeDate`,s.activeDate),py(`selected`,s.selected)(`dateFilter`,s.dateFilter)(`maxDate`,s.maxDate)(`minDate`,s.minDate)(`dateClass`,s.dateClass)(`comparisonStart`,s.comparisonStart)(`comparisonEnd`,s.comparisonEnd)(`startDateAccessibleName`,s.startDateAccessibleName)(`endDateAccessibleName`,s.endDateAccessibleName)(`activeDrag`,s._activeDrag)}}function a(i,c){if(i&1){let s=BC();fa$1(0,`mat-year-view`,5),Hy(`activeDateChange`,function(d){th(s);let m=zC();return _T(m.activeDate,d)||(m.activeDate=d),nh(d)}),Ey(`monthSelected`,function(d){th(s);let m=zC();return nh(m._monthSelectedInYearView(d))})(`selectedChange`,function(d){th(s);let m=zC();return nh(m._goToDateInView(d,`month`))}),Dd()}if(i&2){let s=zC();Vy(`activeDate`,s.activeDate),py(`selected`,s.selected)(`dateFilter`,s.dateFilter)(`maxDate`,s.maxDate)(`minDate`,s.minDate)(`dateClass`,s.dateClass)}}function n(i,c){if(i&1){let s=BC();fa$1(0,`mat-multi-year-view`,6),Hy(`activeDateChange`,function(d){th(s);let m=zC();return _T(m.activeDate,d)||(m.activeDate=d),nh(d)}),Ey(`yearSelected`,function(d){th(s);let m=zC();return nh(m._yearSelectedInMultiYearView(d))})(`selectedChange`,function(d){th(s);let m=zC();return nh(m._goToDateInView(d,`year`))}),Dd()}if(i&2){let s=zC();Vy(`activeDate`,s.activeDate),py(`selected`,s.selected)(`dateFilter`,s.dateFilter)(`maxDate`,s.maxDate)(`minDate`,s.minDate)(`dateClass`,s.dateClass)}}return Ht({type:r,selectors:[[`mat-calendar`]],viewQuery:function(c,s){if(c&1&&wy(Qt,5)(Xt,5)(Zt,5),c&2){let p;qC(p=ZC())&&(s.monthView=p.first),qC(p=ZC())&&(s.yearView=p.first),qC(p=ZC())&&(s.multiYearView=p.first)}},hostAttrs:[1,`mat-calendar`],inputs:{headerComponent:`headerComponent`,startAt:`startAt`,startView:`startView`,selected:`selected`,minDate:`minDate`,maxDate:`maxDate`,dateFilter:`dateFilter`,dateClass:`dateClass`,comparisonStart:`comparisonStart`,comparisonEnd:`comparisonEnd`,startDateAccessibleName:`startDateAccessibleName`,endDateAccessibleName:`endDateAccessibleName`},outputs:{selectedChange:`selectedChange`,yearSelected:`yearSelected`,monthSelected:`monthSelected`,viewChanged:`viewChanged`,_userSelection:`_userSelection`,_userDragDrop:`_userDragDrop`},exportAs:[`matCalendar`],features:[Gy([ta]),ei],decls:5,vars:2,consts:[[3,`cdkPortalOutlet`],[`cdkMonitorSubtreeFocus`,``,`tabindex`,`-1`,1,`mat-calendar-content`],[3,`activeDate`,`selected`,`dateFilter`,`maxDate`,`minDate`,`dateClass`,`comparisonStart`,`comparisonEnd`,`startDateAccessibleName`,`endDateAccessibleName`,`activeDrag`],[3,`activeDate`,`selected`,`dateFilter`,`maxDate`,`minDate`,`dateClass`],[3,`activeDateChange`,`_userSelection`,`dragStarted`,`dragEnded`,`activeDate`,`selected`,`dateFilter`,`maxDate`,`minDate`,`dateClass`,`comparisonStart`,`comparisonEnd`,`startDateAccessibleName`,`endDateAccessibleName`,`activeDrag`],[3,`activeDateChange`,`monthSelected`,`selectedChange`,`activeDate`,`selected`,`dateFilter`,`maxDate`,`minDate`,`dateClass`],[3,`activeDateChange`,`yearSelected`,`selectedChange`,`activeDate`,`selected`,`dateFilter`,`maxDate`,`minDate`,`dateClass`]],template:function(c,s){if(c&1&&(cy(0,e,0,0,`ng-template`,0),fa$1(1,`div`,1),SC(2,t,1,11,`mat-month-view`,2)(3,a,1,6,`mat-year-view`,3)(4,n,1,6,`mat-multi-year-view`,3),Dd()),c&2){let p;py(`cdkPortalOutlet`,s._calendarHeaderPortal),Ab(2),NC((p=s.currentView)===`month`?2:p===`year`?3:p===`multi-year`?4:-1)}},dependencies:[yi,pN,Qt,Xt,Zt],styles:[`.mat-calendar {
  display: block;
  line-height: normal;
  font-family: var(--%NS%mat-datepicker-calendar-text-font, var(--%NS%mat-sys-body-medium-font));
  font-size: var(--%NS%mat-datepicker-calendar-text-size, var(--%NS%mat-sys-body-medium-size));
}

.mat-calendar-header {
  padding: 8px 8px 0 8px;
}

.mat-calendar-content {
  padding: 0 8px 8px 8px;
  outline: none;
}

.mat-calendar-controls {
  display: flex;
  align-items: center;
  margin: 5% calc(4.7142857143% - 16px);
}

.mat-calendar-spacer {
  flex: 1 1 auto;
}

button.mat-calendar-period-button {
  min-width: 0;
  margin: 0 8px;
  font-size: var(--%NS%mat-datepicker-calendar-period-button-text-size, var(--%NS%mat-sys-title-small-size));
  font-weight: var(--%NS%mat-datepicker-calendar-period-button-text-weight, var(--%NS%mat-sys-title-small-weight));
  --%NS%mat-button-text-label-text-color: var(--%NS%mat-datepicker-calendar-period-button-text-color, var(--%NS%mat-sys-on-surface-variant));
}

.mat-calendar-arrow {
  display: inline-block;
  width: 10px;
  height: 5px;
  margin: 0 0 0 5px;
  vertical-align: middle;
  fill: var(--%NS%mat-datepicker-calendar-period-button-icon-color, var(--%NS%mat-sys-on-surface-variant));
}
.mat-calendar-arrow.mat-calendar-invert {
  transform: rotate(180deg);
}
[dir=rtl] .mat-calendar-arrow {
  margin: 0 5px 0 0;
}
@media (forced-colors: active) {
  .mat-calendar-arrow {
    fill: CanvasText;
  }
}

.mat-datepicker-content .mat-calendar-previous-button:not(.mat-mdc-button-disabled),
.mat-datepicker-content .mat-calendar-next-button:not(.mat-mdc-button-disabled) {
  color: var(--%NS%mat-datepicker-calendar-navigation-button-icon-color, var(--%NS%mat-sys-on-surface-variant));
}
[dir=rtl] .mat-calendar-previous-button,
[dir=rtl] .mat-calendar-next-button {
  transform: rotate(180deg);
}

.mat-calendar-table {
  border-spacing: 0;
  border-collapse: collapse;
  width: 100%;
}

.mat-calendar-table-header th {
  text-align: center;
  padding: 0 0 8px 0;
  color: var(--%NS%mat-datepicker-calendar-header-text-color, var(--%NS%mat-sys-on-surface-variant));
  font-size: var(--%NS%mat-datepicker-calendar-header-text-size, var(--%NS%mat-sys-title-small-size));
  font-weight: var(--%NS%mat-datepicker-calendar-header-text-weight, var(--%NS%mat-sys-title-small-weight));
}

.mat-calendar-table-header-divider {
  position: relative;
  height: 1px;
}
.mat-calendar-table-header-divider::after {
  content: "";
  position: absolute;
  top: 0;
  left: -8px;
  right: -8px;
  height: 1px;
  background: var(--%NS%mat-datepicker-calendar-header-divider-color, transparent);
}

.mat-calendar-body-cell-content::before {
  margin: calc(calc(var(--%NS%mat-focus-indicator-border-width, 3px) + 3px) * -1);
}

.mat-calendar-body-cell:focus-visible .mat-focus-indicator::before {
  content: "";
}
`],encapsulation:2})})()}return r})();var ga=new D(`mat-datepicker-scroll-strategy`,{providedIn:`root`,factory:()=>{let r=h(K);return()=>Pt(r)}});var oa=(()=>{class r{_elementRef=h(ce);_animationsDisabled=ZD();_changeDetectorRef=h(mv);_globalModel=h(_e);_dateAdapter=h(v);_ngZone=h($);_rangeSelectionStrategy=h(aa,{optional:!0});_stateChanges;_model;_eventCleanups;_animationFallback;_calendar;color;datepicker;comparisonStart=null;comparisonEnd=null;startDateAccessibleName=null;endDateAccessibleName=null;_isAbove=!1;_animationDone=new B;_isAnimating=!1;_closeButtonText;_closeButtonFocused=!1;_actionsPortal=null;_dialogLabelId=null;constructor(){if(h(or).load(qa),this._closeButtonText=h(ee).closeCalendarLabel,!this._animationsDisabled){let e=this._elementRef.nativeElement,t=h(Yo);this._eventCleanups=this._ngZone.runOutsideAngular(()=>[t.listen(e,`animationstart`,this._handleAnimationEvent),t.listen(e,`animationend`,this._handleAnimationEvent),t.listen(e,`animationcancel`,this._handleAnimationEvent)])}}ngAfterViewInit(){this._stateChanges=this.datepicker.stateChanges.subscribe(()=>{this._changeDetectorRef.markForCheck()}),this._calendar.focusActiveCell()}ngOnDestroy(){clearTimeout(this._animationFallback),this._eventCleanups?.forEach(e=>e()),this._stateChanges?.unsubscribe(),this._animationDone.complete()}_handleUserSelection(e){let t=this._model.selection,a=e.value,n=t instanceof x;if(n&&this._rangeSelectionStrategy){let i=this._rangeSelectionStrategy.selectionFinished(a,t,e.event);this._model.updateSelection(i,this)}else a&&(n||!this._dateAdapter.sameDate(a,t))&&this._model.add(a);(!this._model||this._model.isComplete())&&!this._actionsPortal&&this.datepicker.close()}_handleUserDragDrop(e){this._model.updateSelection(e.value,this)}_startExitAnimation(){this._elementRef.nativeElement.classList.add(`mat-datepicker-content-exit`),this._animationsDisabled?this._animationDone.next():(clearTimeout(this._animationFallback),this._animationFallback=setTimeout(()=>{this._isAnimating||this._animationDone.next()},200))}_handleAnimationEvent=e=>{let t=this._elementRef.nativeElement;e.target!==t||!e.animationName.startsWith(`_mat-datepicker-content`)||(clearTimeout(this._animationFallback),this._isAnimating=e.type===`animationstart`,t.classList.toggle(`mat-datepicker-content-animating`,this._isAnimating),this._isAnimating||this._animationDone.next())};_getSelected(){return this._model.selection}_applyPendingSelection(){this._model!==this._globalModel&&this._globalModel.updateSelection(this._model.selection,this)}_assignActions(e,t){this._model=e?this._globalModel.clone():this._globalModel,this._actionsPortal=e,t&&this._changeDetectorRef.detectChanges()}static ɵfac=function(t){return new(t||r)};static ɵcmp=(function(){function e(t,a){}return Ht({type:r,selectors:[[`mat-datepicker-content`]],viewQuery:function(a,n){if(a&1&&wy(dt,5),a&2){let i;qC(i=ZC())&&(n._calendar=i.first)}},hostAttrs:[1,`mat-datepicker-content`],hostVars:6,hostBindings:function(a,n){a&2&&(Td(n.color?`mat-`+n.color:``),Yr(`mat-datepicker-content-touch`,n.datepicker.touchUi)(`mat-datepicker-content-animations-enabled`,!n._animationsDisabled))},inputs:{color:`color`},exportAs:[`matDatepickerContent`],decls:5,vars:26,consts:[[`cdkTrapFocus`,``,`role`,`dialog`,1,`mat-datepicker-content-container`],[3,`yearSelected`,`monthSelected`,`viewChanged`,`_userSelection`,`_userDragDrop`,`id`,`startAt`,`startView`,`minDate`,`maxDate`,`dateFilter`,`headerComponent`,`selected`,`dateClass`,`comparisonStart`,`comparisonEnd`,`startDateAccessibleName`,`endDateAccessibleName`],[3,`cdkPortalOutlet`],[`type`,`button`,`matButton`,`elevated`,1,`mat-datepicker-close-button`,3,`focus`,`blur`,`click`,`color`]],template:function(a,n){a&1&&(fa$1(0,`div`,0)(1,`mat-calendar`,1),Ey(`yearSelected`,function(c){return n.datepicker._selectYear(c)})(`monthSelected`,function(c){return n.datepicker._selectMonth(c)})(`viewChanged`,function(c){return n.datepicker._viewChanged(c)})(`_userSelection`,function(c){return n._handleUserSelection(c)})(`_userDragDrop`,function(c){return n._handleUserDragDrop(c)}),Dd(),cy(2,e,0,0,`ng-template`,2),fa$1(3,`button`,3),Ey(`focus`,function(){return n._closeButtonFocused=!0})(`blur`,function(){return n._closeButtonFocused=!1})(`click`,function(){return n.datepicker.close()}),yT(4),Dd()()),a&2&&(Yr(`mat-datepicker-content-container-with-custom-header`,n.datepicker.calendarHeaderComponent)(`mat-datepicker-content-container-with-actions`,n._actionsPortal),ci(`aria-modal`,!0)(`aria-labelledby`,n._dialogLabelId??void 0),Ab(),Td(n.datepicker.panelClass),py(`id`,n.datepicker.id)(`startAt`,n.datepicker.startAt)(`startView`,n.datepicker.startView)(`minDate`,n.datepicker._getMinDate())(`maxDate`,n.datepicker._getMaxDate())(`dateFilter`,n.datepicker._getDateFilter())(`headerComponent`,n.datepicker.calendarHeaderComponent)(`selected`,n._getSelected())(`dateClass`,n.datepicker.dateClass)(`comparisonStart`,n.comparisonStart)(`comparisonEnd`,n.comparisonEnd)(`startDateAccessibleName`,n.startDateAccessibleName)(`endDateAccessibleName`,n.endDateAccessibleName),Ab(),py(`cdkPortalOutlet`,n._actionsPortal),Ab(),Yr(`cdk-visually-hidden`,!n._closeButtonFocused),py(`color`,n.color||`primary`),Ab(),Ly(n._closeButtonText))},dependencies:[SN,dt,yi,Mt],styles:[`@keyframes _mat-datepicker-content-dropdown-enter {
  from {
    opacity: 0;
    transform: scaleY(0.8);
  }
  to {
    opacity: 1;
    transform: none;
  }
}
@keyframes _mat-datepicker-content-dialog-enter {
  from {
    opacity: 0;
    transform: scale(0.8);
  }
  to {
    opacity: 1;
    transform: none;
  }
}
@keyframes _mat-datepicker-content-exit {
  from {
    opacity: 1;
  }
  to {
    opacity: 0;
  }
}
.mat-datepicker-content {
  display: block;
  background-color: var(--%NS%mat-datepicker-calendar-container-background-color, var(--%NS%mat-sys-surface-container-high));
  color: var(--%NS%mat-datepicker-calendar-container-text-color, var(--%NS%mat-sys-on-surface));
  box-shadow: var(--%NS%mat-datepicker-calendar-container-elevation-shadow, 0px 0px 0px 0px rgba(0, 0, 0, 0.2), 0px 0px 0px 0px rgba(0, 0, 0, 0.14), 0px 0px 0px 0px rgba(0, 0, 0, 0.12));
  border-radius: var(--%NS%mat-datepicker-calendar-container-shape, var(--%NS%mat-sys-corner-large));
}
.mat-datepicker-content.mat-datepicker-content-animations-enabled {
  animation: _mat-datepicker-content-dropdown-enter 120ms cubic-bezier(0, 0, 0.2, 1);
}
.mat-datepicker-content .mat-calendar {
  width: 296px;
  height: 354px;
}
.mat-datepicker-content .mat-datepicker-content-container-with-custom-header .mat-calendar {
  height: auto;
}
.mat-datepicker-content .mat-datepicker-close-button {
  position: absolute;
  top: 100%;
  left: 0;
  margin-top: 8px;
}
.mat-datepicker-content-animating .mat-datepicker-content .mat-datepicker-close-button {
  display: none;
}

.mat-datepicker-content-container {
  display: flex;
  flex-direction: column;
  justify-content: space-between;
}

.mat-datepicker-content-touch {
  display: block;
  max-height: 80vh;
  box-shadow: var(--%NS%mat-datepicker-calendar-container-touch-elevation-shadow, 0px 0px 0px 0px rgba(0, 0, 0, 0.2), 0px 0px 0px 0px rgba(0, 0, 0, 0.14), 0px 0px 0px 0px rgba(0, 0, 0, 0.12));
  border-radius: var(--%NS%mat-datepicker-calendar-container-touch-shape, var(--%NS%mat-sys-corner-extra-large));
  position: relative;
  overflow: visible;
  min-height: fit-content;
}
.mat-datepicker-content-touch.mat-datepicker-content-animations-enabled {
  animation: _mat-datepicker-content-dialog-enter 150ms cubic-bezier(0, 0, 0.2, 1);
}
.mat-datepicker-content-touch .mat-datepicker-content-container {
  min-height: fit-content;
  max-height: 788px;
  min-width: 250px;
  max-width: 750px;
}
.mat-datepicker-content-touch .mat-calendar {
  width: 100%;
  height: auto;
}

.mat-datepicker-content-exit.mat-datepicker-content-animations-enabled {
  animation: _mat-datepicker-content-exit 100ms linear;
}

@media all and (orientation: landscape) {
  .mat-datepicker-content-touch .mat-datepicker-content-container {
    width: 64vh;
    height: 80vh;
  }
}
@media all and (orientation: portrait) {
  .mat-datepicker-content-touch .mat-datepicker-content-container {
    width: 80vw;
    height: 100vw;
  }
}
`],encapsulation:2})})()}return r})();var Jt=(()=>{class r{_injector=h(K);_viewContainerRef=h(Zr);_dateAdapter=h(v,{optional:!0});_dir=h(cN,{optional:!0});_model=h(_e);_animationsDisabled=ZD();_scrollStrategy=h(ga);_inputStateChanges=Z.EMPTY;_document=h(k);calendarHeaderComponent;get startAt(){return this._startAt||(this.datepickerInput?this.datepickerInput.getStartValue():null)}set startAt(e){this._startAt=this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(e))}_startAt=null;startView=`month`;get color(){return this._color||(this.datepickerInput?this.datepickerInput.getThemePalette():void 0)}set color(e){this._color=e}_color;touchUi=!1;get disabled(){return this._disabled===void 0&&this.datepickerInput?this.datepickerInput.disabled:!!this._disabled}set disabled(e){e!==this._disabled&&(this._disabled=e,this.stateChanges.next(void 0))}_disabled;xPosition=`start`;yPosition=`below`;restoreFocus=!0;yearSelected=new Ee;monthSelected=new Ee;viewChanged=new Ee(!0);dateClass;openedStream=new Ee;closedStream=new Ee;get panelClass(){return this._panelClass}set panelClass(e){this._panelClass=u3(e)}_panelClass;get opened(){return this._opened}set opened(e){e?this.open():this.close()}_opened=!1;id=h(Af).getId(`mat-datepicker-`);_getMinDate(){return this.datepickerInput&&this.datepickerInput.min}_getMaxDate(){return this.datepickerInput&&this.datepickerInput.max}_getDateFilter(){return this.datepickerInput&&this.datepickerInput.dateFilter}_overlayRef=null;_componentRef=null;_focusedElementBeforeOpen=null;_backdropHarnessClass=`${this.id}-backdrop`;_actionsPortal=null;datepickerInput;stateChanges=new B;_changeDetectorRef=h(mv);constructor(){this._dateAdapter,this._model.selectionChanged.subscribe(()=>{this._changeDetectorRef.markForCheck()})}ngOnChanges(e){let t=e.xPosition||e.yPosition;if(t&&!t.firstChange&&this._overlayRef){let a=this._overlayRef.getConfig().positionStrategy;a instanceof lt&&(this._setConnectedPositions(a),this.opened&&this._overlayRef.updatePosition())}this.stateChanges.next(void 0)}ngOnDestroy(){this._destroyOverlay(),this.close(),this._inputStateChanges.unsubscribe(),this.stateChanges.complete()}select(e){this._model.add(e)}_selectYear(e){this.yearSelected.emit(e)}_selectMonth(e){this.monthSelected.emit(e)}_viewChanged(e){this.viewChanged.emit(e)}registerInput(e){return this.datepickerInput,this._inputStateChanges.unsubscribe(),this.datepickerInput=e,this._inputStateChanges=e.stateChanges.subscribe(()=>this.stateChanges.next(void 0)),this._model}registerActions(e){this._actionsPortal,this._actionsPortal=e,this._componentRef?.instance._assignActions(e,!0)}removeActions(e){e===this._actionsPortal&&(this._actionsPortal=null,this._componentRef?.instance._assignActions(null,!0))}open(){this._opened||this.disabled||this._componentRef?.instance._isAnimating||(this.datepickerInput,this._focusedElementBeforeOpen=If(),this._openOverlay(),this._opened=!0,this.openedStream.emit())}close(){if(!this._opened||this._componentRef?.instance._isAnimating)return;let e=this.restoreFocus&&this._focusedElementBeforeOpen&&typeof this._focusedElementBeforeOpen.focus==`function`,t=()=>{this._opened&&(this._opened=!1,this.closedStream.emit())};if(this._componentRef){let{instance:a,location:n}=this._componentRef;a._animationDone.pipe(at$1(1)).subscribe(()=>{let i=this._document.activeElement;e&&(!i||i===this._document.activeElement||n.nativeElement.contains(i))&&this._focusedElementBeforeOpen.focus(),this._focusedElementBeforeOpen=null,this._destroyOverlay()}),a._startExitAnimation()}e?setTimeout(t):t()}_applyPendingSelection(){this._componentRef?.instance?._applyPendingSelection()}_forwardContentValues(e){e.datepicker=this,e.color=this.color,e._dialogLabelId=this.datepickerInput.getOverlayLabelId(),e._assignActions(this._actionsPortal,!1)}_openOverlay(){this._destroyOverlay();let e=this.touchUi,t=new Ct(oa,this._viewContainerRef),a=this._overlayRef=Vt(this._injector,new Z$1({positionStrategy:e?this._getDialogStrategy():this._getDropdownStrategy(),hasBackdrop:!0,backdropClass:[e?`cdk-overlay-dark-backdrop`:`mat-overlay-transparent-backdrop`,this._backdropHarnessClass],direction:this._dir||`ltr`,scrollStrategy:e?te(this._injector):this._scrollStrategy(),panelClass:`mat-datepicker-${e?`dialog`:`popup`}`,disableAnimations:this._animationsDisabled}));this._getCloseStream(a).subscribe(n=>{n&&n.preventDefault(),this.close()}),a.keydownEvents().subscribe(n=>{let i=n.keyCode;(i===38||i===40||i===37||i===39||i===33||i===34)&&n.preventDefault()}),this._componentRef=a.attach(t),this._forwardContentValues(this._componentRef.instance),e||Zl(()=>{a.updatePosition()},{injector:this._injector})}_destroyOverlay(){this._overlayRef&&(this._overlayRef.dispose(),this._overlayRef=this._componentRef=null)}_getDialogStrategy(){return le(this._injector).centerHorizontally().centerVertically()}_getDropdownStrategy(){let e=Dt(this._injector,this.datepickerInput.getConnectedOverlayOrigin()).withTransformOriginOn(`.mat-datepicker-content`).withFlexibleDimensions(!1).withViewportMargin(8).withLockedPosition();return this._setConnectedPositions(e)}_setConnectedPositions(e){let t=this.xPosition===`end`?`end`:`start`,a=t===`start`?`end`:`start`,n=this.yPosition===`above`?`bottom`:`top`,i=n===`top`?`bottom`:`top`;return e.withPositions([{originX:t,originY:i,overlayX:t,overlayY:n},{originX:t,originY:n,overlayX:t,overlayY:i},{originX:a,originY:i,overlayX:a,overlayY:n},{originX:a,originY:n,overlayX:a,overlayY:i}])}_getCloseStream(e){let t=[`ctrlKey`,`shiftKey`,`metaKey`];return NE(e.backdropClick(),e.detachments(),e.keydownEvents().pipe(je(a=>a.keyCode===27&&!HD(a)||this.datepickerInput&&HD(a,`altKey`)&&a.keyCode===38&&t.every(n=>!HD(a,n)))))}static ɵfac=function(t){return new(t||r)};static ɵdir=We({type:r,inputs:{calendarHeaderComponent:`calendarHeaderComponent`,startAt:`startAt`,startView:`startView`,color:`color`,touchUi:[2,`touchUi`,`touchUi`,pn],disabled:[2,`disabled`,`disabled`,pn],xPosition:`xPosition`,yPosition:`yPosition`,restoreFocus:[2,`restoreFocus`,`restoreFocus`,pn],dateClass:`dateClass`,panelClass:`panelClass`,opened:[2,`opened`,`opened`,pn]},outputs:{yearSelected:`yearSelected`,monthSelected:`monthSelected`,viewChanged:`viewChanged`,openedStream:`opened`,closedStream:`closed`},features:[ei]})}return r})();var Cn=(()=>{class r extends Jt{static ɵfac=(()=>{let e;return function(a){return(e||(e=Sg(r)))(a||r)}})();static ɵcmp=Ht({type:r,selectors:[[`mat-datepicker`]],exportAs:[`matDatepicker`],features:[Gy([ta,{provide:Jt,useExisting:r}]),sy],decls:0,vars:0,template:function(t,a){},encapsulation:2})}return r})();var Q=class{target;targetElement;value=null;constructor(o,e){this.target=o,this.targetElement=e,this.value=this.target.value}};var ba=(()=>{class r{_elementRef=h(ce);_dateAdapter=h(v,{optional:!0});_dateFormats=h(W,{optional:!0});_isInitialized=!1;get value(){return this._model?this._getValueFromModel(this._model.selection):this._pendingValue}set value(e){this._assignValueProgrammatically(e,!0)}_model;get disabled(){return!!this._disabled||this._parentDisabled()}set disabled(e){let t=e,a=this._elementRef.nativeElement;this._disabled!==t&&(this._disabled=t,this.stateChanges.next(void 0)),t&&this._isInitialized&&a.blur&&a.blur()}_disabled;dateChange=new Ee;dateInput=new Ee;stateChanges=new B;_onTouched=()=>{};_validatorOnChange=()=>{};_cvaOnChange=()=>{};_valueChangesSubscription=Z.EMPTY;_localeSubscription=Z.EMPTY;_pendingValue=null;_parseValidator=()=>this._lastValueValid?null:{matDatepickerParse:{text:this._elementRef.nativeElement.value}};_filterValidator=e=>{let t=this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(e.value));return!t||this._matchesFilter(t)?null:{matDatepickerFilter:!0}};_minValidator=e=>{let t=this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(e.value)),a=this._getMinDate();return!a||!t||this._dateAdapter.compareDate(a,t)<=0?null:{matDatepickerMin:{min:a,actual:t}}};_maxValidator=e=>{let t=this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(e.value)),a=this._getMaxDate();return!a||!t||this._dateAdapter.compareDate(a,t)>=0?null:{matDatepickerMax:{max:a,actual:t}}};_getValidators(){return[this._parseValidator,this._minValidator,this._maxValidator,this._filterValidator]}_registerModel(e){this._model=e,this._valueChangesSubscription.unsubscribe(),this._pendingValue&&this._assignValue(this._pendingValue),this._valueChangesSubscription=this._model.selectionChanged.subscribe(t=>{if(this._shouldHandleChangeEvent(t)){let a=this._getValueFromModel(t.selection);this._lastValueValid=this._isValidValue(a),this._cvaOnChange(a),this._onTouched(),this._formatValue(a),this.dateInput.emit(new Q(this,this._elementRef.nativeElement)),this.dateChange.emit(new Q(this,this._elementRef.nativeElement))}})}_lastValueValid=!1;constructor(){this._localeSubscription=this._dateAdapter.localeChanges.subscribe(()=>{this._assignValueProgrammatically(this.value,!0)})}ngAfterViewInit(){this._isInitialized=!0}ngOnChanges(e){fa(e,this._dateAdapter)&&this.stateChanges.next(void 0)}ngOnDestroy(){this._valueChangesSubscription.unsubscribe(),this._localeSubscription.unsubscribe(),this.stateChanges.complete()}registerOnValidatorChange(e){this._validatorOnChange=e}validate(e){return this._validator?this._validator(e):null}writeValue(e){this._assignValueProgrammatically(e,e!==this.value)}registerOnChange(e){this._cvaOnChange=e}registerOnTouched(e){this._onTouched=e}setDisabledState(e){this.disabled=e}_onKeydown(e){HD(e,`altKey`)&&e.keyCode===40&&[`ctrlKey`,`shiftKey`,`metaKey`].every(n=>!HD(e,n))&&!this._elementRef.nativeElement.readOnly&&(this._openPopup(),e.preventDefault())}_onInput(e){let t=e.target.value,a=this._lastValueValid,n=this._dateAdapter.parse(t,this._dateFormats.parse.dateInput);this._lastValueValid=this._isValidValue(n),n=this._dateAdapter.getValidDateOrNull(n);let i=!this._dateAdapter.sameDate(n,this.value);!n||i?this._cvaOnChange(n):t&&!this.value&&this._cvaOnChange(n),!i&&a!==this._lastValueValid&&this._validatorOnChange(),i&&(this._assignValue(n),this.dateInput.emit(new Q(this,this._elementRef.nativeElement)))}_onChange(){this.dateChange.emit(new Q(this,this._elementRef.nativeElement))}_onBlur(){this.value&&this._formatValue(this.value),this._onTouched()}_formatValue(e){this._elementRef.nativeElement.value=e!=null?this._dateAdapter.format(e,this._dateFormats.display.dateInput):``}_assignValue(e){this._model?(this._assignValueToModel(e),this._pendingValue=null):this._pendingValue=e}_isValidValue(e){return!e||this._dateAdapter.isValid(e)}_parentDisabled(){return!1}_assignValueProgrammatically(e,t){e=this._dateAdapter.deserialize(e),this._lastValueValid=this._isValidValue(e),e=this._dateAdapter.getValidDateOrNull(e),this._assignValue(e),t&&this._formatValue(e)}_matchesFilter(e){let t=this._getDateFilter();return!t||t(e)}static ɵfac=function(t){return new(t||r)};static ɵdir=We({type:r,inputs:{value:`value`,disabled:[2,`disabled`,`disabled`,pn]},outputs:{dateChange:`dateChange`,dateInput:`dateInput`},features:[ei]})}return r})();function fa(r,o){let e=Object.keys(r);for(let t of e){let{previousValue:a,currentValue:n}=r[t];if(o.isDateInstance(a)&&o.isDateInstance(n)){if(!o.sameDate(a,n))return!0}else return!0}return!1}var ya={provide:U$1,useExisting:vs(()=>sa),multi:!0};var va={provide:A$1,useExisting:vs(()=>sa),multi:!0};var sa=(()=>{class r extends ba{_formField=h(tt$1,{optional:!0});_closedSubscription=Z.EMPTY;_openedSubscription=Z.EMPTY;set matDatepicker(e){e&&(this._datepicker=e,this._ariaOwns.set(e.opened?e.id:null),this._closedSubscription=e.closedStream.subscribe(()=>{this._onTouched(),this._ariaOwns.set(null)}),this._openedSubscription=e.openedStream.subscribe(()=>{this._ariaOwns.set(e.id)}),this._registerModel(e.registerInput(this)))}_datepicker;_ariaOwns=_e$1(null);get min(){return this._min}set min(e){let t=this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(e));this._dateAdapter.sameDate(t,this._min)||(this._min=t,this._validatorOnChange())}_min=null;get max(){return this._max}set max(e){let t=this._dateAdapter.getValidDateOrNull(this._dateAdapter.deserialize(e));this._dateAdapter.sameDate(t,this._max)||(this._max=t,this._validatorOnChange())}_max=null;get dateFilter(){return this._dateFilter}set dateFilter(e){let t=this._matchesFilter(this.value);this._dateFilter=e,this._matchesFilter(this.value)!==t&&this._validatorOnChange()}_dateFilter;_validator=null;constructor(){super(),this._validator=fe.compose(super._getValidators())}getConnectedOverlayOrigin(){return this._formField?this._formField.getConnectedOverlayOrigin():this._elementRef}getOverlayLabelId(){return this._formField?this._formField.getLabelId():this._elementRef.nativeElement.getAttribute(`aria-labelledby`)}getThemePalette(){return this._formField?this._formField.color:void 0}getStartValue(){return this.value}ngOnDestroy(){super.ngOnDestroy(),this._closedSubscription.unsubscribe(),this._openedSubscription.unsubscribe()}_openPopup(){this._datepicker&&this._datepicker.open()}_getValueFromModel(e){return e}_assignValueToModel(e){this._model&&this._model.updateSelection(e,this)}_getMinDate(){return this._min}_getMaxDate(){return this._max}_getDateFilter(){return this._dateFilter}_shouldHandleChangeEvent(e){return e.source!==this}static ɵfac=function(t){return new(t||r)};static ɵdir=We({type:r,selectors:[[`input`,`matDatepicker`,``]],hostAttrs:[1,`mat-datepicker-input`],hostVars:6,hostBindings:function(t,a){t&1&&Ey(`input`,function(i){return a._onInput(i)})(`change`,function(){return a._onChange()})(`blur`,function(){return a._onBlur()})(`keydown`,function(i){return a._onKeydown(i)}),t&2&&(vy(`disabled`,a.disabled),ci(`aria-haspopup`,a._datepicker?`dialog`:null)(`aria-owns`,a._ariaOwns())(`min`,a.min?a._dateAdapter.toIso8601(a.min):null)(`max`,a.max?a._dateAdapter.toIso8601(a.max):null)(`data-mat-calendar`,a._datepicker?a._datepicker.id:null))},inputs:{matDatepicker:`matDatepicker`,min:`min`,max:`max`,dateFilter:[0,`matDatepickerFilter`,`dateFilter`]},exportAs:[`matDatepickerInput`],features:[Gy([ya,va,{provide:Y,useExisting:r}]),sy]})}return r})();var Da=(()=>{class r{static ɵfac=function(t){return new(t||r)};static ɵdir=We({type:r,selectors:[[``,`matDatepickerToggleIcon`,``]]})}return r})();var Ca=(()=>{class r{_intl=h(ee);_changeDetectorRef=h(mv);_stateChanges=Z.EMPTY;datepicker;tabIndex=null;ariaLabel;get disabled(){return this._disabled===void 0&&this.datepicker?this.datepicker.disabled:!!this._disabled}set disabled(e){this._disabled=e}_disabled;disableRipple=!1;_customIcon;_button;constructor(){let e=h(new Na(`tabindex`),{optional:!0}),t=Number(e);this.tabIndex=t||t===0?t:null}ngOnChanges(e){e.datepicker&&this._watchStateChanges()}ngOnDestroy(){this._stateChanges.unsubscribe()}ngAfterContentInit(){this._watchStateChanges()}_open(e){this.datepicker&&!this.disabled&&(this.datepicker.open(),e.stopPropagation())}_watchStateChanges(){let e=this.datepicker?this.datepicker.stateChanges:Ke(),t=this.datepicker&&this.datepicker.datepickerInput?this.datepicker.datepickerInput.stateChanges:Ke(),a=this.datepicker?NE(this.datepicker.openedStream,this.datepicker.closedStream):Ke();this._stateChanges.unsubscribe(),this._stateChanges=NE(this._intl.changes,e,t,a).subscribe(()=>this._changeDetectorRef.markForCheck())}static ɵfac=function(t){return new(t||r)};static ɵcmp=(function(){let e=[`button`],t=[[[``,`matDatepickerToggleIcon`,``]]],a=[`[matDatepickerToggleIcon]`];function n(i,c){i&1&&(ph(),fa$1(0,`svg`,2),hy(1,`path`,3),Dd())}return Ht({type:r,selectors:[[`mat-datepicker-toggle`]],contentQueries:function(c,s,p){if(c&1&&by(p,Da,5),c&2){let d;qC(d=ZC())&&(s._customIcon=d.first)}},viewQuery:function(c,s){if(c&1&&wy(e,5),c&2){let p;qC(p=ZC())&&(s._button=p.first)}},hostAttrs:[1,`mat-datepicker-toggle`],hostVars:8,hostBindings:function(c,s){c&1&&Ey(`click`,function(d){return s._open(d)}),c&2&&(ci(`tabindex`,null)(`data-mat-calendar`,s.datepicker?s.datepicker.id:null),Yr(`mat-datepicker-toggle-active`,s.datepicker&&s.datepicker.opened)(`mat-accent`,s.datepicker&&s.datepicker.color===`accent`)(`mat-warn`,s.datepicker&&s.datepicker.color===`warn`))},inputs:{datepicker:[0,`for`,`datepicker`],tabIndex:`tabIndex`,ariaLabel:[0,`aria-label`,`ariaLabel`],disabled:[2,`disabled`,`disabled`,pn],disableRipple:`disableRipple`},exportAs:[`matDatepickerToggle`],features:[ei],ngContentSelectors:a,decls:4,vars:7,consts:[[`button`,``],[`matIconButton`,``,`type`,`button`,3,`tabIndex`,`disabled`,`disableRipple`],[`viewBox`,`0 0 24 24`,`width`,`24px`,`height`,`24px`,`fill`,`currentColor`,`focusable`,`false`,`aria-hidden`,`true`,1,`mat-datepicker-toggle-default-icon`],[`d`,`M19 3h-1V1h-2v2H8V1H6v2H5c-1.11 0-1.99.9-1.99 2L3 19c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm0 16H5V8h14v11zM7 10h5v5H7z`]],template:function(c,s){c&1&&(wd(t),fa$1(0,`button`,1,0),SC(2,n,2,0,`svg`,2),Cd(3),Dd()),c&2&&(py(`tabIndex`,s.disabled?-1:s.tabIndex)(`disabled`,s.disabled)(`disableRipple`,s.disableRipple),ci(`aria-haspopup`,s.datepicker?`dialog`:null)(`aria-label`,s.ariaLabel||s._intl.openCalendarLabel)(`aria-expanded`,s.datepicker?s.datepicker.opened:null),Ab(2),NC(s._customIcon?-1:2))},dependencies:[mt],styles:[`.mat-datepicker-toggle {
  pointer-events: auto;
  color: var(--%NS%mat-datepicker-toggle-icon-color, var(--%NS%mat-sys-on-surface-variant));
}
.mat-datepicker-toggle button {
  color: inherit;
}

.mat-datepicker-toggle-active {
  color: var(--%NS%mat-datepicker-toggle-active-state-icon-color, var(--%NS%mat-sys-primary));
}

@media (forced-colors: active) {
  .mat-datepicker-toggle-default-icon {
    color: CanvasText;
  }
}
`],encapsulation:2})})()}return r})();var wn=(()=>{class r{static ɵfac=function(t){return new(t||r)};static ɵmod=Ae({type:r});static ɵinj=Ie({providers:[ee],imports:[Bt,we,AN,Zt$1,oa,Ca,ia,Ya,wt]})}return r})();var da=/^\d{4}-\d{2}-\d{2}$/;var wa=[`jan`,`feb`,`mar`,`apr`,`may`,`jun`,`jul`,`aug`,`sep`,`oct`,`nov`,`dec`];var xn={parse:{dateInput:`dd/MM/yyyy`},display:{dateInput:`dd/MM/yyyy`,monthYearLabel:{year:`numeric`,month:`short`},dateA11yLabel:{year:`numeric`,month:`long`,day:`numeric`},monthYearA11yLabel:{year:`numeric`,month:`long`}}};function be(r,o,e){if(r<1e3||r>9999||o<1||o>12||e<1)return null;let t=new Date(r,o-1,e);return t.getFullYear()===r&&t.getMonth()===o-1&&t.getDate()===e?g(t):null}var la=class r extends v{locale$=`en-GB`;getYear(o$1){return o(o$1).getFullYear()}getMonth(o$2){return o(o$2).getMonth()}getDate(o$3){return o(o$3).getDate()}getDayOfWeek(o$4){return o(o$4).getDay()}getMonthNames(o){let e=new Intl.DateTimeFormat(this.locale$,{month:o});return Array.from({length:12},(t,a)=>e.format(new Date(2e3,a,1)))}getDateNames(){return Array.from({length:31},(o,e)=>String(e+1))}getDayOfWeekNames(o){let e=new Intl.DateTimeFormat(this.locale$,{weekday:o});return Array.from({length:7},(t,a)=>e.format(new Date(2e3,0,2+a)))}getYearName(o){return String(this.getYear(o))}getFirstDayOfWeek(){return 1}getNumDaysInMonth(o$5){let e=o(o$5);return new Date(e.getFullYear(),e.getMonth()+1,0).getDate()}clone(o){return o}createDate(o,e,t){if(e<0||e>11)throw Error(`Invalid month index "${e}". Month index has to be between 0 and 11.`);if(t<1)throw Error(`Invalid date "${t}". Date has to be greater than 0.`);let a=be(o,e+1,t);if(!a)throw Error(`Invalid date "${t}" for month with index "${e}".`);return a}today(){return g(/* @__PURE__ */ new Date)}parse(o){if(typeof o!=`string`)return o==null?null:this.invalid();let e=o.trim();if(!e)return null;let t=/^(\d{4})[-/.](\d{1,2})[-/.](\d{1,2})$/.exec(e);if(t)return be(+t[1],+t[2],+t[3])??this.invalid();if(t=/^(\d{1,2})[-/. ](\d{1,2})[-/. ](\d{4})$/.exec(e),t)return be(+t[3],+t[2],+t[1])??this.invalid();if(t=/^(\d{1,2})[-/. ]([a-z]{3})[a-z]*[-/. ,]*(\d{4})$/i.exec(e),t){let a=wa.indexOf(t[2].toLowerCase())+1;return be(+t[3],a,+t[1])??this.invalid()}return this.invalid()}format(o){if(!this.isValid(o))throw Error(`IsoDateAdapter: Cannot format invalid date.`);let[e,t,a]=o.split(`-`);return`${a}/${t}/${e}`}addCalendarYears(o,e){return this.addCalendarMonths(o,e*12)}addCalendarMonths(o$6,e){let t=o(o$6),a=new Date(t.getFullYear(),t.getMonth()+e,1),n=new Date(a.getFullYear(),a.getMonth()+1,0).getDate();return a.setDate(Math.min(t.getDate(),n)),g(a)}addCalendarDays(o$7,e){let t=o(o$7);return t.setDate(t.getDate()+e),g(t)}toIso8601(o){return o}isDateInstance(o){return typeof o==`string`&&da.test(o)}isValid(o){if(!da.test(o))return!1;let[e,t,a]=o.split(`-`).map(Number);return be(e,t,a)!==null}invalid(){return`Invalid date`}deserialize(o){if(o==null||o===``)return null;if(typeof o==`string`){let e=o.slice(0,10);return this.isValid(e)?e:this.invalid()}return this.invalid()}static ɵfac=(()=>{let o;return function(t){return(o||(o=Sg(r)))(t||r)}})();static ɵprov=R({token:r,factory:r.ɵfac})};export{sa as a,xn as c,la as i,Cn as n,v as o,W as r,wn as s,Ca as t};