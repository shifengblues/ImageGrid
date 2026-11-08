import './style.css';
import {layout,placement,equalCuts,regions,integer,moveItem} from './geometry.ts';
import type {Grid,Placement,Size} from './geometry.ts';
import type {Job,Result} from './protocol.ts';
import {pickColor} from './color.ts';

const $=<T extends HTMLElement=HTMLElement>(id:string)=>document.getElementById(id) as T;
type Entry=Size&{file:File;thumbnail:string};
type Option=[string,string];
class SelectBox {
  button=document.createElement('button');menu=document.createElement('div');options:Option[]=[];value='';
  constructor(public key:string,options:Option[],value:string,private change:(value:string)=>void){
    const host=document.querySelector(`[data-select="${key}"]`)!;host.classList.add('select-wrap');
    this.button.type='button';this.button.id=key;this.button.className='select-button';this.button.setAttribute('role','combobox');this.button.setAttribute('aria-label',({theme:'外观',ratio:'单格画幅',mode:'图像处理模式',fit:'缩放方式',format:'合成导出格式',line:'分割线',save:'拆分保存方式'} as Record<string,string>)[key]);
    this.button.setAttribute('aria-haspopup','listbox');this.button.setAttribute('aria-controls',`${key}-options`);this.button.setAttribute('aria-expanded','false');
    this.menu.id=`${key}-options`;this.menu.className='select-menu';this.menu.setAttribute('role','listbox');this.menu.hidden=true;
    host.append(this.button);document.body.append(this.menu);this.setOptions(options);this.set(value);
    this.button.onclick=()=>this.menu.hidden?this.open():this.close();
    this.button.onkeydown=e=>{if(['ArrowDown','ArrowUp'].includes(e.key)){e.preventDefault();this.open();const items=[...this.menu.querySelectorAll('button')];items[Math.max(0,this.options.findIndex(o=>o[0]===this.value))]?.focus();}};
    this.menu.onkeydown=e=>{const items=[...this.menu.querySelectorAll('button')],i=items.indexOf(document.activeElement as HTMLButtonElement);if(e.key==='Escape'){this.close();this.button.focus();}else if(e.key==='ArrowDown'||e.key==='ArrowUp'){e.preventDefault();items[(i+(e.key==='ArrowDown'?1:items.length-1))%items.length]?.focus();}else if(e.key==='Tab')this.close();};
    document.addEventListener('click',e=>{if(!host.contains(e.target as Node)&&!this.menu.contains(e.target as Node))this.close();});
  }
  setOptions(options:Option[]){
    this.options=options;this.menu.replaceChildren();
    for(const [value,label] of options){const button=document.createElement('button');button.className='select-option';button.type='button';button.setAttribute('role','option');button.dataset.value=value;button.textContent=label;button.onclick=()=>{this.set(value);this.close();this.button.focus();this.change(value);};this.menu.append(button);}
  }
  set(value:string){this.value=value;this.button.replaceChildren();const text=document.createElement('span'),arrow=document.createElement('span');text.textContent=this.options.find(o=>o[0]===value)?.[1]??'无内部切分线';arrow.className='select-arrow';arrow.textContent='⌄';this.button.append(text,arrow);for(const option of this.menu.querySelectorAll('button'))option.setAttribute('aria-selected',String(option.dataset.value===value));}
  open(){if(busy)return;document.querySelectorAll<HTMLElement>('.select-menu').forEach(m=>m.hidden=true);const rect=this.button.getBoundingClientRect();this.menu.hidden=false;this.menu.style.width=`${rect.width}px`;this.menu.style.left=`${Math.min(rect.left,innerWidth-rect.width-8)}px`;this.menu.style.top=`${rect.bottom+5}px`;if(this.menu.getBoundingClientRect().bottom>innerHeight-8)this.menu.style.top=`${Math.max(8,rect.top-this.menu.offsetHeight-5)}px`;this.button.setAttribute('aria-expanded','true');}
  close(){this.menu.hidden=true;this.button.setAttribute('aria-expanded','false');}
}
let page:'compose'|'split'='compose',busy=false,entries:Entry[]=[],source:Entry|null=null;
const compose={columns:2,rows:2,ratio:[16,9] as [number,number],gap:3,mode:'auto',fit:'fit',target:{width:1920,height:1080},background:'#FFFFFF',gapColor:'#FFFFFF',format:'png' as 'png'|'jpg'};
const split={columns:2,rows:2,inset:0,xs:[0,1],ys:[0,1]};
let grid:Grid|null=null,validation='',zoom=1,panX=0,panY=0,scale=1,replaceIndex=-1,dragIndex=-1,cancelled=false;
let activeWorker:Worker|null=null,cancelReject:((error:Error)=>void)|null=null;
let activeFallback:AbortController|null=null;
const viewport=$('viewport'),board=$('board'),fileInput=$<HTMLInputElement>('files'),directoryInput=$<HTMLInputElement>('directory');
const selects:Record<string,SelectBox>={};
function select(key:string,options:Option[],value:string,change:(value:string)=>void){return selects[key]=new SelectBox(key,options,value,change);}
const media=matchMedia('(prefers-color-scheme: dark)');let theme='system';
try{theme=localStorage.getItem('image-grid-theme')??'system';}catch{}
if(!['light','dark','system'].includes(theme))theme='system';
function applyTheme(){document.documentElement.dataset.theme=theme==='system'?(media.matches?'dark':'light'):theme;}
applyTheme();media.addEventListener('change',applyTheme);
select('theme',[['light','浅色'],['dark','深色'],['system','跟随系统']],theme,value=>{theme=value;try{localStorage.setItem('image-grid-theme',value);}catch{}applyTheme();});
select('ratio',[['16:9','16:9 / 横向图片'],['9:16','9:16 / 竖向图片'],['1:1','1:1 / 正方形'],['4:3','4:3'],['3:2','3:2'],['custom','自定义比例']],'16:9',value=>{
  $('custom-ratio').hidden=value!=='custom';if(value!=='custom')compose.ratio=value.split(':').map(Number) as [number,number];
  $<HTMLInputElement>('ratio-w').value=String(compose.ratio[0]);$<HTMLInputElement>('ratio-h').value=String(compose.ratio[1]);refresh();
});
select('mode',[['auto','自动填满 · 居中裁切'],['original','原像素 · 不缩放 / 不裁切'],['unified','统一尺寸 · Fit / Fill']],'auto',value=>{compose.mode=value;$('size-settings').hidden=value!=='unified';$('mode-note').textContent=value==='auto'?'小图等比放大、居中填满，比例不同时裁切边缘，不拉伸。':value==='original'?'按源图尺寸居中放置，不缩放、不裁切。PNG 导出保留常见 8 位 PNG 的解码像素。':'按单格宽高等比重采样；Fit 完整显示，Fill 居中裁切。';refresh();});
select('fit',[['fit','Fit · 完整显示，允许留白'],['fill','Fill · 填满，居中裁切']],'fit',value=>{compose.fit=value;refresh();});
select('format',[['png','PNG · 无损'],['jpg','JPG · 最高质量']],'png',value=>compose.format=value as 'png'|'jpg');
select('line',[],'',()=>updateLinePosition());
select('save',[['zip','下载 ZIP 压缩包'],...('showDirectoryPicker' in window?[['folder','保存到本地文件夹'] as Option]:[])],'zip',()=>{});
function status(text:string){$('status').textContent=text;$('status').title=text;}
function setBusy(value:boolean){
  busy=value;$<HTMLFieldSetElement>('settings').disabled=value;document.querySelectorAll<HTMLButtonElement>('[data-idle]').forEach(b=>b.disabled=value);$('processing').hidden=!value;
  if(!value)refresh();
}
function job(request:Job):Promise<Result>{
  return new Promise((resolve,reject)=>{
    const rawPng=request.kind==='split'?/\.png$/i.test(request.file.name):request.kind==='compose'&&request.mode==='original'&&request.format==='png'&&request.files.every(file=>/\.png$/i.test(file.name));
    if(typeof OffscreenCanvas==='undefined'&&!rawPng){
      const controller=new AbortController();activeFallback=controller;cancelReject=reject;
      const progress=(value:number)=>{if(controller.signal.aborted)throw new DOMException('已取消','AbortError');$<HTMLProgressElement>('progress').value=value;};
      void import('./engine.ts').then(({processJob})=>processJob(request,progress)).then(result=>{if(!controller.signal.aborted)resolve(result);},reject).finally(()=>{if(activeFallback===controller){activeFallback=null;cancelReject=null;}});
      return;
    }
    const worker=new Worker(new URL('./worker.ts',import.meta.url),{type:'module'});activeWorker=worker;cancelReject=reject;
    const finish=()=>{worker.terminate();if(activeWorker===worker){activeWorker=null;cancelReject=null;}};
    worker.onmessage=event=>{if(event.data.type==='progress')$<HTMLProgressElement>('progress').value=event.data.value;else if(event.data.type==='result'){finish();resolve(event.data.result);}else{finish();reject(new Error(event.data.message));}};
    worker.onerror=()=>{finish();reject(new Error('浏览器图像处理任务意外结束，可能是内存不足。请减少图片或输出尺寸后重试。'));};worker.postMessage(request);
  });
}
async function prompt(title:string,text:string,confirm=false){
  const dialog=$<HTMLDialogElement>('prompt');$('prompt-title').textContent=title;$('prompt-text').textContent=text;$('prompt-cancel').hidden=!confirm;$('prompt-ok').textContent=confirm?'清空图片':'知道了';
  return new Promise<boolean>(resolve=>{dialog.returnValue='';dialog.addEventListener('close',()=>resolve(dialog.returnValue==='ok'),{once:true});dialog.showModal();});
}
function choose(index=-1){if(busy)return;replaceIndex=index;fileInput.multiple=page==='compose'&&index<0;fileInput.value='';fileInput.click();}
fileInput.onchange=()=>void importFiles([...fileInput.files??[]],replaceIndex);
directoryInput.onchange=()=>void importFiles([...directoryInput.files??[]]);
$('import').onclick=()=>choose();$('empty').onclick=()=>choose();$('folder').onclick=()=>{if(!busy){directoryInput.value='';directoryInput.click();}};
function release(entry:Entry){URL.revokeObjectURL(entry.thumbnail);}
async function importFiles(files:File[],index=-1){
  if(busy||!files.length)return;
  files=files.filter(file=>/\.(png|jpe?g|webp|bmp)$/i.test(file.name)||file.type.startsWith('image/'));
  if(!files.length){await prompt('未找到图片','请选择 PNG、JPG、WebP 或 BMP 图片。文件夹中的其他文件会自动跳过。');return;}
  if(page==='split'&&files.length!==1){await prompt('请选择一张图片','宫格拆分一次处理一张图片，请重新选择。');return;}
  if(files.length>1)index=-1;
  if(page==='compose'&&index<0&&entries.length+files.length>100){await prompt('图片数量过多','一次最多保留 100 张图片，请减少选择的文件。');return;}
  const errors:string[]=[];let success=0;cancelled=false;setBusy(true);
  try{
    for(const file of files){
      if(cancelled)break;
      if(!/\.(png|jpe?g|webp|bmp)$/i.test(file.name)){if(!file.type.startsWith('image/'))continue;errors.push(`${file.name}：目前支持 PNG、JPG、WebP、BMP。`);continue;}
      status(`正在本地读取 ${file.name}…`);
      try{
        const result=await job({kind:'inspect',file});if(cancelled)break;
        const entry:Entry={file,width:result.width!,height:result.height!,thumbnail:URL.createObjectURL(result.thumbnail!)};
        if(page==='split'){
          try{const xs=equalCuts(entry.width,split.columns),ys=equalCuts(entry.height,split.rows);if(source)release(source);source=entry;split.xs=xs;split.ys=ys;}
          catch(error){release(entry);throw error;}
        }
        else if(index>=0){if(entries[index])release(entries[index]);entries[index]=entry;}
        else entries.push(entry);
        success++;refresh();
      }catch(error){if(cancelled)break;errors.push(`${file.name}：${(error as Error).message}`);}
    }
    fit();renderThumbnails();status(`${cancelled?'已取消；':''}已${index>=0?'替换':'导入'} ${success} 张图片${errors.length?`，${errors.length} 张失败`:''}`);
    if(errors.length)await prompt('部分图片无法导入',errors.join('\n'));
  }finally{setBusy(false);}
}
function settings(){return {columns:compose.columns,rows:compose.rows,ratio:compose.ratio,gap:compose.gap,...(compose.mode==='unified'?{target:compose.target}:{})};}
function imageMode():Placement{return compose.mode==='original'?'original':compose.mode==='auto'?'fill':compose.fit as Placement;}
function refresh(){
  validation='';grid=null;
  try{
    if(page==='compose'){
      if(!/^#[0-9a-f]{6}$/i.test(compose.background)||!/^#[0-9a-f]{6}$/i.test(compose.gapColor))throw new Error('颜色请输入 #RRGGBB，例如 #FFFFFF。');
      grid=layout(entries.slice(0,compose.columns*compose.rows),settings());
      if(entries.length>compose.columns*compose.rows)validation=`共 ${entries.length} 张图片，仅有 ${compose.columns*compose.rows} 个格子。请增加行列或删除图片。`;
      $('summary').textContent=entries.length?`${entries.length} 张图片 · ${grid.columns} 列 × ${grid.rows} 行 · 单格 ${grid.cellWidth}×${grid.cellHeight} · 输出 ${grid.width}×${grid.height} px`:'导入图片，开始排列';
    }else{
      integer(split.columns,1,10,'列数');integer(split.rows,1,10,'行数');
      if(source){if(split.xs.length!==split.columns+1||split.ys.length!==split.rows+1)throw new Error('行列超过图片像素尺寸。');const areas=regions(split.xs,split.ys,split.inset);$('summary').textContent=`${source.file.name} · ${source.width}×${source.height} px · ${areas.length} 张图片 · 每边内缩 ${split.inset}px`;}
      else $('summary').textContent='导入一张宫格图片，开始拆分';
    }
  }catch(error){validation=(error as Error).message;}
  if(validation)$('summary').textContent=validation;
  $('summary').classList.toggle('error',!!validation);$('summary').title=$('summary').textContent??'';$('validation').textContent=validation;
  $<HTMLButtonElement>('export').disabled=busy||!!validation||(page==='compose'?!entries.length:!source);
  $('gap-swatch').style.background=compose.gapColor;$('bg-swatch').style.background=compose.background;
  updateLineOptions();renderBoard();
}
function renderBoard(){
  const hasImage=page==='compose'?entries.length>0&&grid!==null:source!==null;
  $('empty').hidden=hasImage;board.hidden=!hasImage;
  $('empty-title').textContent=page==='compose'?'把图片拖到这里':'拖入一张宫格图，开始拆分';
  board.replaceChildren();if(!hasImage){$('zoom').textContent='—';return;}
  board.className=`board ${page==='compose'?'compose-board':'split-board'}`;
  if(page==='compose'&&grid){
    board.style.width=`${grid.width}px`;board.style.height=`${grid.height}px`;board.style.gridTemplateColumns=`repeat(${grid.columns},${grid.cellWidth}px)`;board.style.gridTemplateRows=`repeat(${grid.rows},${grid.cellHeight}px)`;board.style.gap=`${grid.gap}px`;board.style.background=compose.gapColor;
    for(let i=0;i<grid.columns*grid.rows;i++){
      const cell=document.createElement('div');cell.className='cell';cell.style.background=compose.background;cell.dataset.index=String(i);cell.setAttribute('aria-label',`第 ${i+1} 格`);
      const entry=entries[i];
      if(entry){
        const pos=placement(entry.width,entry.height,grid.cellWidth,grid.cellHeight,imageMode());
        if(compose.mode==='original'){const under=document.createElement('div');under.className='checker';Object.assign(under.style,{left:`${pos.x}px`,top:`${pos.y}px`,width:`${pos.width}px`,height:`${pos.height}px`});cell.append(under);}
        const img=document.createElement('img');img.src=entry.thumbnail;img.alt=entry.file.name;img.draggable=false;Object.assign(img.style,{left:`${pos.x}px`,top:`${pos.y}px`,width:`${pos.width}px`,height:`${pos.height}px`});cell.append(img);cell.draggable=true;wireDrag(cell,i);
        const button=document.createElement('button');button.className='replace';button.textContent='替换';button.setAttribute('aria-label',`替换第 ${i+1} 张图片`);button.onclick=()=>choose(i);cell.append(button);
      }else{const hint=document.createElement('span');hint.className='cell-empty';hint.textContent='空格';cell.append(hint);}
      const badge=document.createElement('span');badge.className='badge';badge.textContent=String(i+1).padStart(2,'0');cell.append(badge);cell.ondblclick=()=>choose(i);wireDrop(cell,i);board.append(cell);
    }
  }else if(source){
    board.style.width=`${source.width}px`;board.style.height=`${source.height}px`;board.style.background='white';
    const img=document.createElement('img');img.src=source.thumbnail;img.alt=source.file.name;img.className='split-image';img.draggable=false;board.append(img);
    const svg=document.createElementNS('http://www.w3.org/2000/svg','svg');svg.classList.add('split-svg');svg.setAttribute('viewBox',`0 0 ${source.width} ${source.height}`);svg.innerHTML=split.xs.slice(1,-1).map((x,i)=>cutLine('x',i+1,x,source!.height)).join('')+split.ys.slice(1,-1).map((y,i)=>cutLine('y',i+1,y,source!.width)).join('');
    try{if(split.inset>0)for(const area of regions(split.xs,split.ys,split.inset)){const rect=document.createElementNS(svg.namespaceURI,'rect');for(const [key,value] of Object.entries(area))rect.setAttribute(key,String(value));rect.setAttribute('class','inset-box');svg.append(rect);}}catch{}
    board.append(svg);
  }
  transformBoard();
}
function cutLine(axis:string,index:number,position:number,length:number){const coordinates=axis==='x'?`x1="${position}" y1="0" x2="${position}" y2="${length}"`:`x1="0" y1="${position}" x2="${length}" y2="${position}"`;return `<line ${coordinates} class="cut-line ${selects.line.value===`${axis}:${index}`?'selected':''}"/><line ${coordinates} class="cut-hit ${axis==='x'?'vertical':'horizontal'}" data-cut="${axis}:${index}"/>`;}
function contentSize():Size{return page==='compose'&&grid?grid:source??{width:1,height:1};}
function fitScale(){const size=contentSize();return Math.max(.00001,Math.min((viewport.clientWidth-54)/size.width,(viewport.clientHeight-54)/size.height));}
function transformBoard(){
  if(board.hidden)return;scale=fitScale()*zoom;board.style.transform=`translate(-50%,-50%) translate(${-panX}px,${-panY}px) scale(${scale})`;board.style.setProperty('--scale',String(scale));
  // Use image-space stroke widths: CSS-transform hit testing otherwise selects the wrong line on tiny images.
  board.querySelectorAll<SVGElement>('.cut-hit').forEach(line=>line.style.strokeWidth=String(18/scale));
  board.querySelectorAll<SVGElement>('.cut-line').forEach(line=>line.style.strokeWidth=String((line.classList.contains('selected')?3:2)/scale));
  board.querySelectorAll<SVGElement>('.inset-box').forEach(rect=>rect.style.strokeWidth=String(1/scale));
  $('zoom').textContent=`${Math.round(scale*devicePixelRatio*100)}%`;
}
function fit(){zoom=1;panX=panY=0;transformBoard();}
$('fit').onclick=fit;$('pixel').onclick=()=>{if(board.hidden)return;zoom=1/(fitScale()*devicePixelRatio);panX=panY=0;transformBoard();};new ResizeObserver(()=>transformBoard()).observe(viewport);
viewport.addEventListener('wheel',e=>{if(board.hidden)return;e.preventDefault();const old=zoom;zoom=Math.max(.1,Math.min(1000,zoom*(e.deltaY<0?1.15:1/1.15)));const rect=viewport.getBoundingClientRect(),x=e.clientX-rect.left-rect.width/2,y=e.clientY-rect.top-rect.height/2;panX=(panX+x)*(zoom/old)-x;panY=(panY+y)*(zoom/old)-y;transformBoard();},{passive:false});
viewport.oncontextmenu=e=>e.preventDefault();
let panStart:{x:number;y:number;px:number;py:number}|null=null,cutDrag:string|null=null;
viewport.onpointerdown=e=>{
  if(busy)return;
  if(e.button===1||e.button===2){e.preventDefault();panStart={x:e.clientX,y:e.clientY,px:panX,py:panY};viewport.setPointerCapture(e.pointerId);}
  const line=(e.target as Element).closest<SVGElement>('[data-cut]');if(e.button===0&&line){cutDrag=line.dataset.cut!;selects.line.set(cutDrag);viewport.setPointerCapture(e.pointerId);e.preventDefault();updateLinePosition();}
};
viewport.onpointermove=e=>{
  if(panStart){panX=panStart.px-e.clientX+panStart.x;panY=panStart.py-e.clientY+panStart.y;transformBoard();}
  if(cutDrag&&source){const [axis,indexText]=cutDrag.split(':'),index=Number(indexText),array=axis==='x'?split.xs:split.ys,rect=board.getBoundingClientRect();const value=Math.round(axis==='x'?(e.clientX-rect.left)/rect.width*source.width:(e.clientY-rect.top)/rect.height*source.height);const min=array[index-1]+2*split.inset+1,max=array[index+1]-2*split.inset-1;if(min<=max){array[index]=Math.max(min,Math.min(max,value));refresh();}}
};
viewport.onpointerup=viewport.onpointercancel=e=>{panStart=null;cutDrag=null;if(viewport.hasPointerCapture(e.pointerId))viewport.releasePointerCapture(e.pointerId);};
function wireDrag(element:HTMLElement,index:number){element.ondragstart=e=>{if(busy){e.preventDefault();return;}dragIndex=index;e.dataTransfer!.setData('application/x-image-grid',String(index));e.dataTransfer!.effectAllowed='move';};element.ondragend=()=>{dragIndex=-1;document.querySelectorAll('.drop-target').forEach(e=>e.classList.remove('drop-target'));};}
function wireDrop(element:HTMLElement,index:number){
  element.ondragover=e=>{if(busy)return;e.preventDefault();e.stopPropagation();element.classList.add('drop-target');};element.ondragleave=()=>element.classList.remove('drop-target');
  element.ondrop=e=>{e.preventDefault();e.stopPropagation();element.classList.remove('drop-target');if(busy)return;if(e.dataTransfer?.files.length)void importFiles([...e.dataTransfer.files],e.dataTransfer.files.length===1?index:-1);else if(dragIndex>=0){entries=moveItem(entries,dragIndex,index);dragIndex=-1;refresh();renderThumbnails();}};
}
viewport.ondragover=e=>{if(busy)return;e.preventDefault();viewport.classList.add('dragover');$('drop-hint').hidden=false;};viewport.ondragleave=e=>{if(!viewport.contains(e.relatedTarget as Node)){viewport.classList.remove('dragover');$('drop-hint').hidden=true;}};
viewport.ondrop=e=>{e.preventDefault();viewport.classList.remove('dragover');$('drop-hint').hidden=true;if(e.dataTransfer?.files.length)void importFiles([...e.dataTransfer.files]);};
document.addEventListener('dragover',e=>{if(e.dataTransfer?.types.includes('Files'))e.preventDefault();});document.addEventListener('drop',e=>{if(e.dataTransfer?.types.includes('Files')){e.preventDefault();if(!viewport.contains(e.target as Node)&&!$('thumbnails').contains(e.target as Node))void importFiles([...e.dataTransfer.files]);}});
function renderThumbnails(){
  const list=$('thumbnails');list.replaceChildren();
  entries.forEach((entry,index)=>{
    const card=document.createElement('div');card.className='thumbnail';card.draggable=true;card.setAttribute('aria-label',`图片 ${index+1}：${entry.file.name}`);
    const visual=document.createElement('div');visual.className='thumbnail-visual';const img=document.createElement('img');img.src=entry.thumbnail;img.alt='';img.draggable=false;visual.append(img);
    const remove=document.createElement('button');remove.className='remove';remove.textContent='×';remove.setAttribute('aria-label',`删除第 ${index+1} 张图片`);remove.onclick=()=>{if(busy)return;release(entry);entries.splice(index,1);refresh();renderThumbnails();status(`已移除 ${entry.file.name}，原文件未删除`);};visual.append(remove);
    const title=document.createElement('p');title.textContent=`${String(index+1).padStart(2,'0')}  ${entry.file.name}`;title.title=entry.file.name;const dimensions=document.createElement('small');dimensions.textContent=`${entry.width} × ${entry.height}`;card.append(visual,title,dimensions);card.ondblclick=()=>choose(index);wireDrag(card,index);wireDrop(card,index);list.append(card);
  });
}
function setPage(value:'compose'|'split'){
  if(busy)return;page=value;document.querySelectorAll<HTMLButtonElement>('[data-page]').forEach(b=>{b.classList.toggle('active',b.dataset.page===value);b.setAttribute('aria-current',b.dataset.page===value?'page':'false');});
  $('page-title').textContent=page==='compose'?'宫格合成':'宫格拆分';$('compose-settings').hidden=page==='split';$('split-settings').hidden=page==='compose';$('thumbnails').hidden=page==='split';$('split-help').hidden=page==='compose';$('format-settings').hidden=page==='split';$('save-settings').hidden=page==='compose';$('folder').hidden=page==='split';
  $('export').innerHTML=page==='compose'?'导出宫格图 <span>↗</span>':'导出全部图片 <span>↗</span>';
  $<HTMLInputElement>('columns').value=String((page==='compose'?compose:split).columns);$<HTMLInputElement>('rows').value=String((page==='compose'?compose:split).rows);refresh();fit();
}
document.querySelectorAll<HTMLButtonElement>('[data-page]').forEach(b=>b.onclick=()=>setPage(b.dataset.page as 'compose'|'split'));
function updateCount(axis:'columns'|'rows',value:number){const state=page==='compose'?compose:split;if(state[axis]===value)return;state[axis]=value;if(page==='split'&&source&&Number.isInteger(value)&&value>=1&&value<=10){try{if(axis==='columns')split.xs=equalCuts(source.width,value);else split.ys=equalCuts(source.height,value);}catch{}}refresh();}
for(const axis of ['columns','rows'] as const)$<HTMLInputElement>(axis).oninput=e=>updateCount(axis,Number((e.target as HTMLInputElement).value||NaN));
document.querySelectorAll<HTMLButtonElement>('[data-preset]').forEach(b=>b.onclick=()=>{const value=Number(b.dataset.preset);updateCount('columns',value);updateCount('rows',value);$<HTMLInputElement>('columns').value=String(value);$<HTMLInputElement>('rows').value=String(value);fit();});
function numberInput(id:string,set:(value:number)=>void){$<HTMLInputElement>(id).oninput=e=>{const value=(e.target as HTMLInputElement).value;set(value===''?NaN:Number(value));refresh();};}
numberInput('gap',v=>{compose.gap=v;$<HTMLInputElement>('gap-slider').value=String(v);});numberInput('gap-slider',v=>{compose.gap=v;$<HTMLInputElement>('gap').value=String(v);});
numberInput('ratio-w',v=>compose.ratio[0]=v);numberInput('ratio-h',v=>compose.ratio[1]=v);numberInput('target-w',v=>compose.target.width=v);numberInput('target-h',v=>compose.target.height=v);numberInput('inset',v=>split.inset=v);
for(const [id,key] of [['gap-color','gapColor'],['background','background']] as const)$<HTMLInputElement>(id).oninput=e=>{compose[key]=(e.target as HTMLInputElement).value;refresh();};
for(const [id,key,input] of [['pick-gap','gapColor','gap-color'],['pick-background','background','background']] as const)$(id).onclick=async()=>{const value=await pickColor(compose[key]);if(value){compose[key]=value;$<HTMLInputElement>(input).value=value;refresh();}};
function updateLineOptions(){
  const options:Option[]=[...split.xs.slice(1,-1).map((_,i)=>[`x:${i+1}`,`纵向分割线 ${i+1}`] as Option),...split.ys.slice(1,-1).map((_,i)=>[`y:${i+1}`,`横向分割线 ${i+1}`] as Option)];
  if(JSON.stringify(selects.line.options)!==JSON.stringify(options)){selects.line.setOptions(options);selects.line.set(options.some(o=>o[0]===selects.line.value)?selects.line.value:options[0]?.[0]??'');}
  if(page==='split')updateLinePosition();
}
function updateLinePosition(){const [axis,index]=selects.line.value.split(':');$<HTMLInputElement>('line-position').value=String((axis==='x'?split.xs:split.ys)[Number(index)]??'');}
$('apply-line').onclick=()=>{if(!source||!selects.line.value)return;const [axis,indexText]=selects.line.value.split(':'),index=Number(indexText),array=axis==='x'?split.xs:split.ys,value=Number($<HTMLInputElement>('line-position').value);const min=array[index-1]+2*split.inset+1,max=array[index+1]-2*split.inset-1;if(!Number.isInteger(value)||value<min||value>max){void prompt('坐标超出范围',`此分割线可设为 ${min}–${max} 的整数像素坐标。`);return;}array[index]=value;refresh();};
$('reset-cuts').onclick=()=>{if(!source)return;try{split.xs=equalCuts(source.width,split.columns);split.ys=equalCuts(source.height,split.rows);refresh();status('已恢复均匀等分');}catch(error){void prompt('无法等分',(error as Error).message);}};
$('clear').onclick=async()=>{if(busy||!(page==='compose'?entries.length:source))return;if(await prompt('清空图片','清空已导入的图片？原始文件不会被删除。',true)){if(page==='compose'){entries.forEach(release);entries=[];renderThumbnails();}else{release(source!);source=null;}refresh();fit();status('已清空图片');}};
$('privacy').onclick=()=>void prompt('图片只在你的设备上处理','选择的图片仅在当前浏览器内读取、拼接和拆分，不发送到服务器。\n\nGitHub Pages 负责提供网页程序；本工具不接入账号、图片上传接口或统计脚本。\n\n关闭或刷新网页后，图片和布局不会保存。预览使用缩略图，导出重新读取源图片。');
function download(blob:Blob,name:string){const url=URL.createObjectURL(blob),anchor=document.createElement('a');anchor.href=url;anchor.download=name;document.body.append(anchor);anchor.click();anchor.remove();setTimeout(()=>URL.revokeObjectURL(url),60000);}
interface Writable {write(data:Blob):Promise<void>;close():Promise<void>}
interface Folder {getDirectoryHandle(name:string,options:{create:boolean}):Promise<Folder>;getFileHandle(name:string,options:{create:boolean}):Promise<{createWritable():Promise<Writable>}>}
$('export').onclick=async()=>{
  if(busy)return;refresh();if(validation||page==='compose'&&!entries.length||page==='split'&&!source)return;
  cancelled=false;let folder:Folder|null=null;
  try{
    setBusy(true);
    if(page==='split'&&selects.save.value==='folder')folder=await (window as unknown as {showDirectoryPicker(options:{mode:string}):Promise<Folder>}).showDirectoryPicker({mode:'readwrite'});
    setBusy(true);status(page==='compose'?'正在从原图生成宫格…':'正在从原图拆分图片…');$<HTMLProgressElement>('progress').value=0;
    const result=await job(page==='compose'?{kind:'compose',files:entries.map(e=>e.file),sizes:entries.map(e=>({width:e.width,height:e.height})),grid:grid!,mode:imageMode(),background:compose.background,gapColor:compose.gapColor,format:compose.format}:{kind:'split',file:source!.file,xs:[...split.xs],ys:[...split.ys],inset:split.inset,archive:!folder});
    if(cancelled)return;
    if(folder&&result.files){const name=`ImageGrid_${new Date().toISOString().replace(/[:.]/g,'-')}_${crypto.randomUUID().slice(0,6)}`;const output=await folder.getDirectoryHandle(name,{create:true});let saved=0;for(const entry of result.files){if(cancelled)break;const handle=await output.getFileHandle(entry.name,{create:true}),stream=await handle.createWritable();await stream.write(entry.blob);await stream.close();saved++;}status(`${cancelled?'已取消；':''}已保存 ${saved} 张图片至所选文件夹 / ${name}`);}
    else if(result.blob){download(result.blob,result.name!);status(`已生成 ${result.name} · ${(result.blob.size/1048576).toFixed(2)} MB，请查看浏览器下载`);}
  }catch(error){if(cancelled||(error as Error).name==='AbortError')status('已取消');else{status(`处理失败：${(error as Error).message}`);await prompt('处理失败',(error as Error).message);}}
  finally{setBusy(false);}
};
$('cancel').onclick=()=>{cancelled=true;activeWorker?.terminate();activeWorker=null;activeFallback?.abort();cancelReject?.(new DOMException('已取消','AbortError'));cancelReject=null;status('正在取消…');};
refresh();
