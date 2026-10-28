'use strict';
const $ = id => document.getElementById(id);
const scene = $('scene'), ctx = scene.getContext('2d');
const chart = $('chart'), graph = chart.getContext('2d');
const colors = ['#00dfb4','#5ba9ff','#f8ba6b','#df85ff','#ff748e'];
let maps = [], selected = 0, currentMap = null, result = null;
let playing = false, simTime = 0, duration = 1, lastFrame = 0, speed = 2;
let yaw = -.74, pitch = .57, zoom = 1, pointer = null;

function resized(canvas, g) {
  const r = canvas.getBoundingClientRect(), dpr = Math.min(window.devicePixelRatio || 1, 2);
  const w = Math.max(1, Math.round(r.width * dpr)), h = Math.max(1, Math.round(r.height * dpr));
  if (canvas.width !== w || canvas.height !== h) { canvas.width = w; canvas.height = h; }
  g.setTransform(dpr, 0, 0, dpr, 0, 0);
  return [r.width, r.height];
}
const lerp = (a,b,t) => a + (b-a)*t;
function norm(v) { return Math.hypot(v.x, v.y, v.z); }
function dist(a,b) { return Math.hypot(a.x-b.x,a.y-b.y,a.z-b.z); }
function pointOnRoute(route, t) {
  if (t < route.startDelay) return null;
  let traveled = (t-route.startDelay)*speed, path=route.path;
  if (traveled >= route.length) return null; // достиг цели — исчезает
  for (let i=1;i<path.length;i++) {
    const d=dist(path[i-1],path[i]);
    if (traveled<=d) {
      const u=d?traveled/d:0;
      return {x:lerp(path[i-1].x,path[i].x,u),y:lerp(path[i-1].y,path[i].y,u),z:lerp(path[i-1].z,path[i].z,u)};
    }
    traveled-=d;
  }
  return null;
}
function projector(w,h,map) {
  const {min,max} = map;
  if (!map.is3D) {
    const width=max.x-min.x,height=max.y-min.y,sc=Math.min((w-82)/width,(h-68)/height)*zoom;
    const cx=(max.x+min.x)/2,cy=(max.y+min.y)/2;
    return p=>({x:w/2+(p.x-cx)*sc,y:h/2-(p.y-cy)*sc,depth:0});
  }
  const dx=max.x-min.x,dy=max.y-min.y,dz=max.z-min.z;
  const scale=Math.min((w-95)/(Math.max(dx,dy)*1.35),(h-70)/(Math.max(dx,dy)*.85+dz*1.4))*zoom;
  const cy=Math.cos(yaw),sy=Math.sin(yaw),cp=Math.cos(pitch),sp=Math.sin(pitch);
  const origin={x:(min.x+max.x)/2,y:(min.y+max.y)/2,z:(min.z+max.z)/2};
  return p=>{
    const x=p.x-origin.x,y=p.y-origin.y,z=p.z-origin.z;
    const across=x*cy+y*sy, depth=-x*sy+y*cy;
    return {x:w/2+across*scale,y:h*.55-(depth*sp+z*cp*1.4)*scale,depth:depth*cp-z*sp};
  };
}
function strokePath(g, points, project, color, width=2.8, dash=[]) {
  if (!points || points.length<2) return;
  g.strokeStyle=color;g.lineWidth=width;g.lineJoin='round';g.lineCap='round';g.setLineDash(dash);
  g.beginPath();let p=project(points[0]);g.moveTo(p.x,p.y);
  for(let i=1;i<points.length;i++){p=project(points[i]);g.lineTo(p.x,p.y);}
  g.stroke();g.setLineDash([]);
}
function disc(g,p,r,fill,stroke=''){
  g.beginPath();g.arc(p.x,p.y,r,0,Math.PI*2);g.fillStyle=fill;g.fill();
  if(stroke){g.strokeStyle=stroke;g.lineWidth=1.5;g.stroke();}
}
function grid2D(g, map, proj){
  const lo=map.min,hi=map.max;
  g.strokeStyle='#20334b';g.lineWidth=1;
  for(let x=Math.ceil(lo.x);x<=hi.x;x+=2){strokePath(g,[{x,y:lo.y,z:0},{x,y:hi.y,z:0}],proj,'#1c2b41',.8);}
  for(let y=Math.ceil(lo.y);y<=hi.y;y+=2){strokePath(g,[{x:lo.x,y,z:0},{x:hi.x,y,z:0}],proj,'#1c2b41',.8);}
  g.font='11px system-ui';g.fillStyle='#6e87a1';g.textAlign='center';
  for(let x=Math.ceil(lo.x/5)*5;x<=hi.x;x+=5){let p=proj({x,y:lo.y,z:0});g.fillText(`${x}`,p.x,p.y+17);}
  g.textAlign='right';
  for(let y=Math.ceil(lo.y/5)*5;y<=hi.y;y+=5){let p=proj({x:lo.x,y,z:0});g.fillText(`${y}`,p.x-9,p.y+4);}
  g.strokeStyle='#6a829a';const a=proj({x:lo.x,y:lo.y,z:0}),b=proj({x:hi.x,y:hi.y,z:0});
  g.strokeRect(a.x,b.y,b.x-a.x,a.y-b.y);
}
function boxFaces(box){
  let a=box.min,b=box.max;
  let p=[{x:a.x,y:a.y,z:a.z},{x:b.x,y:a.y,z:a.z},{x:b.x,y:b.y,z:a.z},{x:a.x,y:b.y,z:a.z},
         {x:a.x,y:a.y,z:b.z},{x:b.x,y:a.y,z:b.z},{x:b.x,y:b.y,z:b.z},{x:a.x,y:b.y,z:b.z}];
  return [[0,1,2,3],[4,5,6,7],[0,1,5,4],[1,2,6,5],[2,3,7,6],[3,0,4,7]].map((face,i)=>({p:face.map(k=>p[k]),face:i}));
}
function grid3D(g, map, proj){
  const a=map.min,b=map.max,z=a.z;
  const step = Math.max(2, Math.round(Math.max(b.x-a.x,b.y-a.y)/10));
  for(let x=a.x;x<=b.x;x+=step)strokePath(g,[{x,y:a.y,z},{x,y:b.y,z}],proj,'#22334a',1);
  for(let y=a.y;y<=b.y;y+=step)strokePath(g,[{x:a.x,y,z},{x:b.x,y,z}],proj,'#22334a',1);
  const o={x:a.x,y:a.y,z:a.z};
  strokePath(g,[o,{x:b.x,y:a.y,z:a.z}],proj,'#c17572',1.8);
  strokePath(g,[o,{x:a.x,y:b.y,z:a.z}],proj,'#72bb98',1.8);
  strokePath(g,[o,{x:a.x,y:a.y,z:b.z}],proj,'#86afff',1.8);
  g.font='bold 12px system-ui';g.fillStyle='#dd8585';let p=proj({x:b.x,y:a.y,z:a.z});g.fillText('X',p.x+6,p.y);
  g.fillStyle='#77c8a3';p=proj({x:a.x,y:b.y,z:a.z});g.fillText('Y',p.x+6,p.y);
  g.fillStyle='#8db4ff';p=proj({x:a.x,y:a.y,z:b.z});g.fillText('Z',p.x+6,p.y);
}
function draw3DObstacles(g, map, proj){
  const all=[];
  for (const [i,box] of map.boxes.entries()) for(const f of boxFaces(box)){
    const pts=f.p.map(proj);all.push({pts,index:i,face:f.face,depth:pts.reduce((a,p)=>a+p.depth,0)/pts.length});
  }
  all.sort((a,b)=>b.depth-a.depth);
  const fills=['rgba(244,155,86,.18)','rgba(251,163,92,.40)','rgba(249,169,107,.25)',
               'rgba(234,130,75,.33)','rgba(203,103,65,.34)','rgba(244,154,81,.28)'];
  for (const f of all){
    g.beginPath();g.moveTo(f.pts[0].x,f.pts[0].y);
    for(let i=1;i<f.pts.length;i++)g.lineTo(f.pts[i].x,f.pts[i].y);
    g.closePath();g.fillStyle=fills[f.face];g.fill();g.lineWidth=.8;g.strokeStyle='rgba(255,175,98,.48)';g.stroke();
  }
}
function drawScene(){
  const [w,h]=resized(scene,ctx);
  ctx.clearRect(0,0,w,h);ctx.fillStyle='#0b1525';ctx.fillRect(0,0,w,h);
  if (!currentMap) return;
  const map=currentMap,p=projector(w,h,map);
  if (map.is3D){grid3D(ctx,map,p);draw3DObstacles(ctx,map,p);}
  else {
    grid2D(ctx,map,p);
    for(const c of map.circles){disc(ctx,p(c.center),Math.hypot(p(c.center).x-p({x:c.center.x+c.radius,y:c.center.y,z:0}).x,p(c.center).y-p({x:c.center.x+c.radius,y:c.center.y,z:0}).y),'rgba(250,155,71,.35)','#f6a65e');}
  }
  const routes = result?.routes ?? map.agents.map((pair,i)=>({id:i+1,start:pair.start,goal:pair.goal,path:[]}));
  for(let i=0;i<routes.length;i++){
    const r=routes[i], col=colors[i%colors.length];
    if($('showInitial').checked && r.initialPath) strokePath(ctx,r.initialPath,p,'rgba(166,191,220,.75)',1.4,[6,6]);
    if(r.path.length>1)strokePath(ctx,r.path,p,col,2.8);
    let sp=p(r.start),gp=p(r.goal);
    disc(ctx,sp,5,'#111b2c',col);disc(ctx,gp,5,col,'#f5fbff');
    ctx.font='11px system-ui';ctx.fillStyle=col;ctx.fillText(`${r.id}`,sp.x+9,sp.y-9);
    if(result){const pos=pointOnRoute(r,simTime);if(pos){let a=p(pos);disc(ctx,a,10,col+'33');disc(ctx,a,5.4,col,'white');}}
  }
}
function drawChart(){
  const [w,h]=resized(chart,graph);graph.clearRect(0,0,w,h);graph.fillStyle='#111b2c';graph.fillRect(0,0,w,h);
  let routes=result?.routes.filter(r=>r.convergence?.length>1) ?? [];
  const l=48,r=w-14,t=12,b=h-26;
  if(routes.length===0){graph.fillStyle='#8296b0';graph.font='12px system-ui';graph.fillText('Запустите оптимизацию для просмотра графика',15,h/2);return;}
  let min=Math.min(...routes.flatMap(x=>x.convergence)),max=Math.max(...routes.flatMap(x=>x.convergence));
  if(max-min<.001)max=min+1;
  const count=Math.max(...routes.map(x=>x.convergence.length));
  graph.font='10px system-ui';graph.fillStyle='#7d90a8';
  for(let j=0;j<=4;j++){
    let val=lerp(min,max,j/4),y=b-(b-t)*j/4;
    graph.strokeStyle='#27364b';graph.beginPath();graph.moveTo(l,y);graph.lineTo(r,y);graph.stroke();
    graph.fillText(val.toFixed(1),3,y+3);
  }
  for(let j=0;j<=4;j++)graph.fillText(String(Math.round((count-1)*j/4)), l+(r-l)*j/4-5,h-8);
  routes.forEach(route=>{
    let values=route.convergence;graph.strokeStyle=colors[(route.id-1)%colors.length];graph.lineWidth=1.8;graph.beginPath();
    values.forEach((v,i)=>{
      let x=l+(r-l)*i/(count-1),y=b-(b-t)*(v-min)/(max-min);
      if(i===0)graph.moveTo(x,y);else graph.lineTo(x,y);
    });graph.stroke();
  });
}
function writeStats(){
  if(!result)return;
  const total=result.routes.reduce((s,r)=>s+r.length,0), safe=result.routes.filter(r=>r.collisionFree).length;
  const last=result.routes[0]?.convergence?.at(-1);
  $('stats').innerHTML=`<div class="stat"><small>СУММА ДЛИН МАРШРУТОВ</small><strong>${total.toFixed(1)}</strong></div>`+
    `<div class="stat"><small>ВРЕМЯ РАСЧЁТА</small><strong>${(result.elapsedMs/1000).toFixed(2)} с</strong></div>`+
    `<div class="stat"><small>БЕЗ СТОЛКНОВЕНИЙ</small><strong>${safe} / ${result.routes.length}</strong></div>`+
    `<div class="stat"><small>СТОИМОСТЬ · БПЛА 1</small><strong>${last===undefined?'—':last.toFixed(1)}</strong></div>`;
  $('routes').innerHTML=result.routes.map((r,i)=>`<div class="route-row"><i class="route-dot" style="background:${colors[i%colors.length]}"></i>`+
    `<div class="route-info">БПЛА ${r.id}<small>${r.collisionFree?'Допустимый путь':'Путь с пересечениями'} · задержка ${r.startDelay.toFixed(2)} с</small></div>`+
    `<div class="route-number">${r.length.toFixed(2)} ед.<br><small style="color:#8397af">A*: ${r.initialLength.toFixed(2)}</small></div></div>`).join('');
  const okay=result.allSafe && result.agentsSeparated;
  $('safetyBadge').className='badge '+(okay?'good':'bad');
  $('safetyBadge').textContent=okay?'✓ Без столкновений':'⚠ Есть проблемы';
  $('warnings').hidden=result.warnings.length===0;
  $('warnings').textContent=result.warnings.join('\n');
}
function updateTime(){
  $('time').value=String(Math.round(simTime/duration*1000));
  $('timeLabel').textContent=simTime.toFixed(1)+' с';
}
async function loadMap(id){
  selected=id;playing=false;result=null;simTime=0;zoom=1;
  $('play').disabled=true;$('reset').disabled=true;$('csv').disabled=true;$('time').disabled=true;
  $('play').textContent='▶ Пуск';$('safetyBadge').textContent='Ожидание';$('safetyBadge').className='badge';$('warnings').hidden=true;
  for(let btn of document.querySelectorAll('.map-btn'))btn.classList.toggle('active',Number(btn.dataset.id)===id);
  try{
    const response=await fetch('/api/map/'+id);
    if(!response.ok)throw new Error('Не удалось загрузить карту');
    currentMap=await response.json();
    $('sceneTitle').textContent=currentMap.name;
    $('sceneDesc').textContent=id===0?'1 агент · 3 круглых препятствия · пространство 30 × 30':
      `${currentMap.agents.length} БПЛА · ${currentMap.boxes.length} препятствий · 3D планирование`;
    $('hint').textContent=id===0?'2D · фиксированный вид':'3D · мышь: вращение / колесо: масштаб';
    $('stats').innerHTML='<div class="stat"><small>ДЛИНА МАРШРУТОВ</small><strong>—</strong></div>'.repeat(4);
    $('routes').innerHTML='<span class="muted">Нет результатов.</span>';
    drawScene();drawChart();
  }catch(e){$('status').textContent=e.message;$('status').className='status error';}
}
async function run(){
  const b=$('run');b.disabled=true;b.textContent='Выполняется оптимизация…';
  $('status').className='status';$('status').textContent='Расчёт A* + SSA, проверка траекторий и столкновений…';
  playing=false;
  try{
    const request={mapId:selected,population:Number($('population').value),iterations:Number($('iterations').value),
      seed:Number($('seed').value),speed:Number($('speed').value),separation:Number($('separation').value),
      smooth:$('smooth').checked,repairInvalidStarts:$('repair').checked};
    const response=await fetch('/api/run',{method:'POST',headers:{'Content-Type':'application/json'},body:JSON.stringify(request)});
    const json=await response.json();if(!response.ok)throw Error(json.error||'Ошибка сервера');
    result=json;currentMap=json.map;speed=request.speed;simTime=0;
    duration=Math.max(1,...result.routes.map(r=>r.startDelay+r.length/speed+.5));
    $('play').disabled=false;$('reset').disabled=false;$('csv').disabled=false;$('time').disabled=false;
    $('status').textContent=`Готово: ${json.iterations} итераций, ${json.population} сальп, ${(json.elapsedMs/1000).toFixed(2)} с.`;
    writeStats();updateTime();drawScene();drawChart();
  }catch(e){$('status').textContent=String(e.message||e);$('status').className='status error';}
  finally{b.disabled=false;b.textContent='Выполнить оптимизацию →';}
}
function frame(now){
  const dt=lastFrame?Math.min(.05,(now-lastFrame)/1000):0;lastFrame=now;
  if(playing){simTime+=dt;if(simTime>=duration){simTime=duration;playing=false;$('play').textContent='▶ Пуск';}updateTime();drawScene();}
  requestAnimationFrame(frame);
}
function exportCsv(){
  if(!result)return;
  const lines=['map_id,uav_id,initial_length,ssa_length,improvement_pct,cost,sharp_turns,start_delay_s,collision_free,elapsed_ms'];
  for(const r of result.routes){
    const gain=100*(r.initialLength-r.length)/Math.max(1e-10,r.initialLength);
    lines.push([result.map.id,r.id,r.initialLength.toFixed(5),r.length.toFixed(5),gain.toFixed(3),r.cost.toFixed(5),r.turns,r.startDelay.toFixed(3),r.collisionFree,result.elapsedMs.toFixed(2)].join(','));
  }
  const blob=new Blob([lines.join('\n')],{type:'text/csv;charset=utf-8'});
  const url=URL.createObjectURL(blob),link=document.createElement('a');link.href=url;link.download=`ssa_map_${result.map.id}.csv`;
  document.body.appendChild(link);link.click();link.remove();setTimeout(()=>URL.revokeObjectURL(url),1000);
}
function attach(){
  $('run').addEventListener('click',run);
  for(const field of ['population','iterations'])$(field).addEventListener('input',()=>$(field+'Out').textContent=$(field).value);
  $('play').addEventListener('click',()=>{if(simTime>=duration)simTime=0;playing=!playing;$('play').textContent=playing?'Ⅱ Пауза':'▶ Пуск';});
  $('reset').addEventListener('click',()=>{playing=false;simTime=0;updateTime();drawScene();$('play').textContent='▶ Пуск';});
  $('csv').addEventListener('click',exportCsv);
  $('time').addEventListener('input',()=>{playing=false;simTime=duration*Number($('time').value)/1000;updateTime();drawScene();$('play').textContent='▶ Пуск';});
  $('showInitial').addEventListener('change',drawScene);
  scene.addEventListener('pointerdown',e=>{if(currentMap?.is3D){pointer={x:e.clientX,y:e.clientY};scene.setPointerCapture(e.pointerId);}});
  scene.addEventListener('pointermove',e=>{if(!pointer||!currentMap?.is3D)return;yaw+=(e.clientX-pointer.x)*.008;pitch=Math.max(.17,Math.min(1.45,pitch-(e.clientY-pointer.y)*.006));pointer={x:e.clientX,y:e.clientY};drawScene();});
  scene.addEventListener('pointerup',()=>{pointer=null;});
  scene.addEventListener('pointercancel',()=>{pointer=null;});
  scene.addEventListener('wheel',e=>{if(!currentMap?.is3D)return;e.preventDefault();zoom=Math.max(.4,Math.min(2.8,zoom*Math.exp(-e.deltaY*.001)));drawScene();},{passive:false});
  window.addEventListener('resize',()=>{drawScene();drawChart();});
}
async function main(){
  attach();requestAnimationFrame(frame);
  try{
    const response=await fetch('/api/maps');maps=await response.json();
    $('map-tabs').innerHTML=maps.map(m=>`<button class="map-btn" data-id="${m.id}">${m.id===0?'Эксперимент 1':`Карта ${m.id}`}<span>${m.is3D?'3D':'2D'} · ${m.agents} БПЛА</span></button>`).join('');
    document.querySelectorAll('.map-btn').forEach(btn=>btn.addEventListener('click',()=>loadMap(Number(btn.dataset.id))));
    await loadMap(0);
  }catch(err){$('status').textContent='Сервер недоступен: '+err.message;}
}
main();
