'use strict';
const $ = id => document.getElementById(id);
const canvas = $('scene'), ctx = canvas.getContext('2d');
const palette = ['#6ce4e0', '#cbb8f8', '#e4b78b', '#91dda8', '#f39fba'];
let state = null, previous = null, receivedAt = 0, isBusy = false;
let azimuth = -0.67, elevation = 0.72, zoom = 1.0, topView = false;
let width = 1000, height = 700, mouse = null;
const v = p => [p.x, p.y, p.z];
const mix = (a,b,t) => a.map((x,i) => x*(1-t)+b[i]*t);
const dist = (a,b) => Math.hypot(a[0]-b[0],a[1]-b[1],a[2]-b[2]);
const base = [40,40,3];

function resize(){
  const rect = canvas.getBoundingClientRect();
  const dpr = Math.min(2, window.devicePixelRatio||1);
  width = rect.width; height = rect.height;
  canvas.width = Math.ceil(width*dpr); canvas.height = Math.ceil(height*dpr);
  ctx.setTransform(dpr,0,0,dpr,0,0);
}
window.addEventListener('resize',resize); resize();

function projection(p){
  const right = [-Math.sin(azimuth),Math.cos(azimuth),0];
  const up = [-Math.cos(azimuth)*Math.sin(elevation),-Math.sin(azimuth)*Math.sin(elevation),Math.cos(elevation)];
  const d = p.map((x,i)=>x-base[i]);
  const scale = Math.min(width/134, height/103)*zoom;
  return [width*0.5+scale*dot(d,right),height*0.52-scale*dot(d,up)];
}
function dot(a,b){return a[0]*b[0]+a[1]*b[1]+a[2]*b[2]}
function line(points,color,stroke=1,dash=[],alpha=1){
  if(points.length<2)return;
  ctx.save();ctx.beginPath();ctx.moveTo(...projection(points[0]));
  for(let i=1;i<points.length;i++)ctx.lineTo(...projection(points[i]));
  ctx.strokeStyle=color;ctx.lineWidth=stroke;ctx.globalAlpha=alpha;ctx.setLineDash(dash);ctx.stroke();ctx.restore();
}
function poly(points,fill,outline=null){
  ctx.beginPath();ctx.moveTo(...projection(points[0]));
  for(let i=1;i<points.length;i++)ctx.lineTo(...projection(points[i]));
  ctx.closePath();if(fill){ctx.fillStyle=fill;ctx.fill()}if(outline){ctx.strokeStyle=outline;ctx.lineWidth=.8;ctx.stroke()}
}
function circle(point,size,color,edge=null,glow=0){
  const [x,y]=projection(point);
  ctx.save();if(glow){ctx.shadowColor=color;ctx.shadowBlur=glow}
  ctx.beginPath();ctx.arc(x,y,size,0,Math.PI*2);ctx.fillStyle=color;ctx.fill();
  if(edge){ctx.lineWidth=1.5;ctx.strokeStyle=edge;ctx.stroke()}ctx.restore();
}
function label(text,point,color='#a0b7c9',size=10){
  const [x,y]=projection(point);ctx.fillStyle=color;ctx.font=`${size}px IBM Plex Mono, Consolas, monospace`;ctx.textAlign='center';ctx.fillText(text,x,y);
}

function drawGround(){
  poly([[0,0,0],[80,0,0],[80,80,0],[0,80,0]],'rgba(12,30,42,.56)','#375263');
  for(let t=0;t<=80;t+=5){
    const major=t%20===0;
    line([[t,0,0],[t,80,0]],major?'#355464':'#213946',major?0.9:.6,[],major?.85:.65);
    line([[0,t,0],[80,t,0]],major?'#355464':'#213946',major?0.9:.6,[],major?.85:.65);
    if(t%20===0){label(String(t),[t,-2,0],'#6d8a9d',9);label(String(t),[-2,t,0],'#6d8a9d',9)}
  }
  line([[0,0,0],[0,0,12]],'#b0d4e3',1.3);
  label('Z', [0,0,13.0],'#aec8d8',11);
  label('X', [83,0,0],'#aec8d8',11);
  label('Y', [0,83,0],'#aec8d8',11);
}
function obstDistance(p,o){
  let sq=0;
  for(let i=0;i<3;i++){
    const mi=v(o.min)[i],ma=v(o.max)[i],a=p[i];
    sq+=Math.pow(Math.max(mi-a,0,a-ma),2);
  }
  return Math.sqrt(sq);
}
function drawPotential(s){
  // Плоский срез поля для иллюстрации: притяжение к цели + отталкивание от AABB.
  const z=1.0,goal=v(s.goal);
  for(let y=0;y<80;y+=4){for(let x=0;x<80;x+=4){
    const p=[x+2,y+2,z];
    let u=0.0025*Math.pow(dist(p,goal),2);
    for(const o of s.obstacles){const d=Math.max(.3,obstDistance(p,o));
      if(d<8)u+=8*Math.pow(1/d-1/8,2);}
    const factor=Math.min(1,u/20);
    const r=Math.round(55+205*factor),b=Math.round(220-170*factor);
    poly([[x,y,.04],[x+4,y,.04],[x+4,y+4,.04],[x,y+4,.04]],`rgba(${r},${Math.round(135-65*factor)},${b},.38)`);
  }}
}
function drawBoxes(s){
  const faces=[];
  for(const o of s.obstacles){
    const [x,y,z]=v(o.min),[X,Y,Z]=v(o.max);
    const corners=[[x,y,z],[X,y,z],[X,Y,z],[x,Y,z],[x,y,Z],[X,y,Z],[X,Y,Z],[x,Y,Z]];
    const definitions=[[0,1,5,4],[1,2,6,5],[2,3,7,6],[3,0,4,7],[4,5,6,7]];
    const faceColors=['#7893a85a','#6d859e70','#89a3b76c','#627a8f77','#b4c9d979'];
    for(let i=0;i<definitions.length;i++){
      const pts=definitions[i].map(n=>corners[n]);
      const avg=pts.reduce((a,p)=>a.map((x,j)=>x+p[j]/4),[0,0,0]);
      const eye=[Math.cos(azimuth)*Math.cos(elevation),Math.sin(azimuth)*Math.cos(elevation),Math.sin(elevation)];
      faces.push({pts, color:faceColors[i],depth:dot(avg,eye),edge:'#7d9bad74'});
    }
  }
  faces.sort((a,b)=>a.depth-b.depth);
  for(const f of faces)poly(f.pts,f.color,f.edge);
  // Маленькие метки имён препятствий выводим только в плане сверху.
  if(elevation>1.30)for(const o of s.obstacles){
    const a=v(o.min),b=v(o.max);
    label(o.name,[(a[0]+b[0])/2,(a[1]+b[1])/2,b[2]+.6],'#dae5ed',9);
  }
}
function pathPts(points){return points.map(v)}
function drawRoutes(s){
  if($('showPaths').checked){
    line(pathPts(s.astarPath),'#e5a16b',1.7,[6,5],.85);
    line(pathPts(s.thetaPath),'#50e3d0',2.1,[],.85);
  }else{
    line(pathPts(s.path),s.planner==='theta'?'#50e3d0':'#e5a16b',2.6,[],.95);
  }
  for(const [point,clr,word] of [[v(s.start),'#80d7e0','СТАРТ'],[v(s.goal),'#f2c97b','ЦЕЛЬ']]){
    line([[point[0],point[1],0],point],clr,1,[4,3],.8);
    circle(point,5,clr,'#edf9f8',12);label(word,[point[0],point[1],point[2]+2.1],clr,10);
  }
}
function animatedPosition(drone,index){
  if(!previous||previous.version!==state.version||!previous.drones[index])return v(drone.position);
  // Сглаживаем между двумя последними состояниями, не предсказывая будущую позицию.
  const t=Math.min(1,(performance.now()-receivedAt)/200);
  return mix(v(previous.drones[index].position),v(drone.position),t);
}
function drawDrones(s){
  const ordered=s.drones.map((d,i)=>({d,i,pos:animatedPosition(d,i)}));
  const eye=[Math.cos(azimuth)*Math.cos(elevation),Math.sin(azimuth)*Math.cos(elevation),Math.sin(elevation)];
  ordered.sort((a,b)=>dot(a.pos,eye)-dot(b.pos,eye));
  for(const {d,i,pos} of ordered){
    const col=palette[i%palette.length];
    if($('showTrails').checked)line(pathPts(d.trail),col,1.8,[],.7);
    line([[pos[0],pos[1],0],pos],col,.9,[3,3],.5);
    circle([pos[0],pos[1],.05],2.6,'#2f5e65a8');
    circle(pos,7.6,col,'#f0fcff',11);
    const [sx,sy]=projection(pos);
    ctx.save();ctx.fillStyle='#07121b';ctx.font='700 10px IBM Plex Mono, Consolas, monospace';
    ctx.textAlign='center';ctx.textBaseline='middle';ctx.fillText(String(d.id),sx,sy+.2);ctx.restore();
  }
  const l=v(s.leader),[lx,ly]=projection(l);
  ctx.save();ctx.strokeStyle='#f7dd8c';ctx.lineWidth=2;ctx.setLineDash([3,3]);
  ctx.beginPath();ctx.arc(lx,ly,13,0,Math.PI*2);ctx.stroke();ctx.restore();
  circle(l,2,'#ffe69c',null,8);
}
function draw(){
  ctx.clearRect(0,0,width,height);
  if(!state){requestAnimationFrame(draw);return}
  drawGround();if($('showField').checked)drawPotential(state);
  drawBoxes(state);drawRoutes(state);drawDrones(state);
  requestAnimationFrame(draw);
}
requestAnimationFrame(draw);

function updateUI(s){
  $('loading').style.display='none';
  $('scenarioLabel').textContent=s.scenarioName;
  $('time').innerHTML=`${s.simulationTime.toFixed(1)} <small>с</small>`;
  $('remaining').innerHTML=`${s.metrics.leaderDistanceRemaining.toFixed(1)} <small>м</small>`;
  $('dronesCount').innerHTML=`${s.drones.length} <small>шт.</small>`;
  $('waypoints').textContent=s.path.length;
  $('flightStatus').textContent=s.arrived?'Цель достигнута. Рой удерживает строй.':
    (s.running?'Рой движется к цели':'Симуляция на паузе')+` · Блокированные шаги: ${s.metrics.blockedSteps}`;
  $('status').textContent=s.arrived?'ФИНИШ':s.running?'РАБОТАЕТ':'ПАУЗА';
  $('play').textContent=s.running?'Ⅱ Пауза':'▶ Продолжить';
  $('aLen').textContent=s.metrics.astar.length.toFixed(1);
  $('tLen').textContent=s.metrics.theta.length.toFixed(1);
  $('aTurns').textContent=s.metrics.astar.turns;
  $('tTurns').textContent=s.metrics.theta.turns;
  $('aNodes').textContent=s.metrics.astar.expanded.toLocaleString('ru-RU');
  $('tNodes').textContent=s.metrics.theta.expanded.toLocaleString('ru-RU');
  $('aMs').textContent=s.metrics.astar.milliseconds.toFixed(1);
  $('tMs').textContent=s.metrics.theta.milliseconds.toFixed(1);
  if (document.activeElement !== $('speed')) $('speed').value = String(s.speed);
  $('speedVal').textContent=s.speed.toFixed(2).replace(/0$/,'')+'×';
}
async function poll(){
  if(isBusy)return;
  isBusy=true;
  try{
    const response=await fetch('/api/state',{cache:'no-store'});
    if(!response.ok)throw new Error('HTTP '+response.status);
    const next=await response.json();
    previous=state;state=next;receivedAt=performance.now();updateUI(next);
  }catch(error){$('loading').style.display='grid';$('loading').textContent='Нет связи с сервером: '+error.message}
  finally{isBusy=false}
}
async function post(path,data){
  try{
    const response=await fetch(path,{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(data)});
    if(!response.ok)throw new Error((await response.text()).slice(0,300));
    previous=null;state=await response.json();receivedAt=performance.now();updateUI(state);
  }catch(e){alert('Ошибка: '+e.message)}
}
$('play').addEventListener('click',()=>state&&post('/api/control',{running:!state.running}));
$('reset').addEventListener('click',()=>post('/api/reset',{scenario:$('scenario').value,planner:$('planner').value}));
$('scenario').addEventListener('change',()=>post('/api/reset',{scenario:$('scenario').value,planner:$('planner').value}));
$('planner').addEventListener('change',()=>post('/api/reset',{scenario:$('scenario').value,planner:$('planner').value}));
$('speed').addEventListener('input',()=>{$('speedVal').textContent=Number($('speed').value).toFixed(2)+'×';post('/api/control',{speed:Number($('speed').value)})});
$('topview').addEventListener('click',()=>{
  topView=!topView;elevation=topView?Math.PI/2:0.72;
  $('topview').textContent=topView?'◈ Вид под углом':'⊞ Вид сверху';
});
window.addEventListener('keydown',e=>{
  if(['INPUT','SELECT','TEXTAREA'].includes(document.activeElement?.tagName))return;
  if(e.code==='Space'){e.preventDefault();$('play').click()}
  if(e.code==='KeyR')$('reset').click();
  if(e.code==='KeyV')$('topview').click();
});
canvas.addEventListener('pointerdown',e=>{mouse=[e.clientX,e.clientY];canvas.setPointerCapture(e.pointerId)});
canvas.addEventListener('pointermove',e=>{
  if(!mouse)return;
  azimuth+=(e.clientX-mouse[0])*.0065;
  elevation=Math.min(Math.PI/2,Math.max(.14,elevation+(e.clientY-mouse[1])*.005));
  mouse=[e.clientX,e.clientY];topView=false;
  $('topview').textContent='⊞ Вид сверху';
});
canvas.addEventListener('pointerup',()=>mouse=null);
canvas.addEventListener('pointercancel',()=>mouse=null);
canvas.addEventListener('wheel',e=>{e.preventDefault();zoom=Math.max(.45,Math.min(2.9,zoom*Math.exp(-e.deltaY*.0008)))},{passive:false});
setInterval(poll,200);poll();
