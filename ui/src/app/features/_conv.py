import re,sys
def convert_fields(block):
    # wrap each mat-form-field having a mat-label into app-filter-field
    pat=re.compile(r'( *)<mat-form-field([^>]*)>\s*<mat-label>(.*?)</mat-label>(.*?)</mat-form-field>',re.S)
    def rep(m):
        ind,attrs,label,body=m.groups()
        attrs=re.sub(r'\s*class="filters__field"','',attrs)
        body=body.replace('<mat-select','<mat-select placeholder="All" aria-label="%s"'%label,1) if '<mat-select' in body else body.replace('<input','<input aria-label="%s"'%label,1)
        return f'{ind}<app-filter-field label="{label}">\n{ind}  <mat-form-field{attrs}>{body}</mat-form-field>\n{ind}</app-filter-field>'
    return pat.sub(rep,block)
def run(path, searchlabel_re=True):
    s=open(path,encoding='utf-8').read()
    # locate pieces
    bar_start=s.index('<section class="filters"')
    sec_end=s.index('</section>',bar_start)+len('</section>')
    sec=s[bar_start:sec_end]
    search=re.search(r'<app-search-field.*?/>',sec,re.S).group(0)
    search=search.replace(' class="filters__search"','')
    # fields: everything in bar after search up to toggle button + panel contents
    bar_inner=sec[sec.index('/>',sec.index('<app-search-field'))+2: sec.index('<button\n      mat-stroked-button')]
    panel_m=re.search(r'@if \(panelOpen\(\)\) \{\s*<div class="filters__panel"[^>]*>(.*?)\n    </div>\n  \}',sec,re.S)
    panel=panel_m.group(1) if panel_m else ''
    chips_m=re.search(r'  @if \(activeChips\(\)\.length > 0\) \{.*?\n  \}\n',sec,re.S)
    chips=chips_m.group(0) if chips_m else ''
    fields=convert_fields(bar_inner+panel)
    new=f'''<div class="filters" role="search" aria-label="Filters">
  {search}
  <app-filter-popup [activeCount]="filterCount()" (cleared)="query.reset()">
{fields}
  </app-filter-popup>
</div>
{chips.replace("class=\"filters__active\"","class=\"filters__active\"")}'''
    s=s[:bar_start]+new+s[sec_end:]
    open(path,'w',encoding='utf-8').write(s)
run(sys.argv[1])
