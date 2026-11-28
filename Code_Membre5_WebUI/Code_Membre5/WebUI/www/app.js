// Interface PressiGest : aucune règle métier ici. Chaque action appelle Member5Service (base PostgreSQL) via WebBridge.cs.
const ST={Deposee:'Déposée',EnLavage:'En lavage',EnRepassage:'En repassage',Prete:'Prête',Avertie:'Avertie',Retiree:'Retirée',Vendue:'Vendue',Annulee:'Annulée',EnLivraison:'En livraison',Livree:'Livrée'};
const MODES={Especes:'Espèces',OrangeMoney:'Orange Money',MtnMobileMoney:'MTN Mobile Money'};
const META={retrait:['Retrait','Rechercher un ticket et enregistrer le paiement du solde','01'],ventes:['Ventes','Articles arrivés au terme du délai de grâce','02'],notifs:['Notifications & emails','Alertes à traiter et journal des envois','03']};
const S={user:null,db:'',err:'',lg:'',view:'retrait',q:'',sel:null,sel2:null,tab:'notifs',d:{recherche:[],eligibles:[],notifications:[],emails:[]}};

// Communication avec C# (WebView2)
const host=window.chrome&&window.chrome.webview;let seq=0;const pend=new Map();
if(host)host.addEventListener('message',e=>{const r=JSON.parse(e.data),p=pend.get(r.id);pend.delete(r.id);r.ok?p.res(r.result):p.rej(r.error)});
const call=(method,args={})=>new Promise((res,rej)=>{if(!host)return rej('Interface à ouvrir depuis l\'application PressiGest (WebView2).');const id=++seq;pend.set(id,{res,rej});host.postMessage(JSON.stringify({id,method,args}))});
const refresh=async()=>{try{S.d=await call('state',{term:S.q})}catch(e){toast(String(e),true)}render()};
const act=async(fn,msg)=>{try{await fn();if(msg)toast(msg)}catch(e){toast(typeof e==='string'?e:String(e),true)}await refresh()};

const fmt=n=>Number(n).toLocaleString('fr-FR')+' FCFA';
const dt=t=>t?new Date(t).toLocaleDateString('fr-FR')+' '+new Date(t).toLocaleTimeString('fr-FR',{hour:'2-digit',minute:'2-digit'}):'—';
const d0=t=>t?new Date(t).toLocaleDateString('fr-FR'):'—';
const esc=s=>String(s??'').replace(/[&<>"]/g,c=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;'}[c]));
const badge=s=>`<span class="badge b-${s}">${ST[s]}</span>`;
const $=s=>document.querySelector(s);
function toast(msg,err){const t=document.createElement('div');t.className='t'+(err?' err':'');t.textContent=msg;$('#toast').append(t);setTimeout(()=>t.remove(),3800)}
function ask(titre,msg,ok){const m=$('#modal');m.hidden=false;m.innerHTML=`<div class="dlg"><h3>${esc(titre)}</h3><p>${esc(msg)}</p><div class="row"><button class="btn ghost" id="mn">Annuler</button><button class="btn" id="my">Confirmer</button></div></div>`;
 $('#mn').onclick=()=>m.hidden=true;$('#my').onclick=()=>{m.hidden=true;ok()}}
const modeOpts=()=>Object.entries(MODES).map(([k,v])=>`<option value="${k}">${v}</option>`).join('');
const empty=(ic,t,s)=>`<div class="empty"><div class="ic">${ic}</div><b>${t}</b><div>${s}</div></div>`;

const allowed=()=>!S.user?[]:S.user.role==='Administrateur'?['retrait','ventes','notifs']:S.user.role==='Caissier'?['retrait']:[];
function nav(){
 const nb=S.d.notifications.length,ne=S.d.eligibles.length;
 $('#nav').innerHTML=Object.entries(META).filter(([k])=>allowed().includes(k)).map(([k,[t,,n]])=>`<button data-v="${k}" class="${S.view===k?'on':''}"><span class="n">${n}</span>${k==='notifs'?'Notifications':t}${k==='notifs'&&nb?`<span class="cnt">${nb}</span>`:''}${k==='ventes'&&ne?`<span class="cnt">${ne}</span>`:''}</button>`).join('');
}
function vRetrait(){
 const list=S.d.recherche;
 if(!list.some(c=>c.id===S.sel))S.sel=list[0]?.id??null;
 const c=list.find(x=>x.id===S.sel);
 const left=`<div class="card tw">${list.length?`<table><thead><tr><th>Ticket</th><th>Client</th><th>Téléphone</th><th>Statut</th><th class="r">Total</th></tr></thead><tbody>${list.map(x=>`<tr data-id="${x.id}" class="${x.id===S.sel?'sel':''}"><td class="mono">${x.ticket}</td><td>${esc(x.nom)}</td><td>${esc(x.tel)}</td><td>${badge(x.statut)}</td><td class="r">${fmt(x.total)}</td></tr>`).join('')}</tbody></table>`:empty('⌕',S.q.trim()?'Aucune commande trouvée':'Recherchez une commande','Saisissez un numéro de ticket (PR-…), un nom ou un téléphone.')}</div>`;
 const right=c?`<div class="card panel"><h3>${c.ticket}</h3><div>${badge(c.statut)}</div>
 <div class="kpis"><div class="kpi"><small>Total</small><b>${c.total.toLocaleString('fr-FR')}</b></div><div class="kpi"><small>Déjà payé</small><b>${c.paye.toLocaleString('fr-FR')}</b></div><div class="kpi hl"><small>Reste</small><b>${c.reste.toLocaleString('fr-FR')}</b></div></div>
 <dl class="meta"><dt>Client</dt><dd>${esc(c.nom)} · ${esc(c.tel)}</dd><dt>Service</dt><dd>${esc(c.service)}</dd><dt>Articles</dt><dd>${esc(c.articles.join(', '))}</dd><dt>Prête le</dt><dd>${d0(c.prete)}</dd></dl>
 <div class="form"><div class="row"><label class="f">Montant (FCFA)<input type="number" id="amt" min="0" max="${c.reste}" value="${c.reste}"></label><label class="f">Mode<select id="mode">${modeOpts()}</select></label></div>
 <div class="row"><button class="btn ghost" id="pay">Enregistrer paiement</button><button class="btn" id="out">Valider retrait</button></div></div></div>`
 :`<div class="card">${empty('◧','Aucune commande sélectionnée','Le détail et le paiement apparaîtront ici.')}</div>`;
 return `<div class="card bar"><div class="search"><input id="q" placeholder="Ticket PR-… ou téléphone" value="${esc(S.q)}" autofocus></div><button class="btn" id="go">Rechercher</button></div><div style="height:18px"></div><div class="grid">${left}${right}</div>`;
}
function vVentes(){
 const l=S.d.eligibles;if(!l.some(c=>c.id===S.sel2))S.sel2=l[0]?.id??null;
 return `<div class="card bar"><button class="btn ghost" id="rf">Actualiser</button><label class="f" style="margin-left:auto">Prix de vente (FCFA)<input type="number" id="prix" min="0" value="0"></label><label class="f">Mode<select id="mode">${modeOpts()}</select></label><button class="btn" id="sell" ${l.length?'':'disabled'} style="align-self:end">Marquer vendue</button></div><div style="height:18px"></div>
 <div class="card tw">${l.length?`<table><thead><tr><th>Ticket</th><th>Client</th><th>Téléphone</th><th>Avertie le</th><th>Vendable le</th><th class="r">Total</th></tr></thead><tbody>${l.map(c=>`<tr data-id="${c.id}" class="${S.sel2===c.id?'sel':''}"><td class="mono">${c.ticket}</td><td>${esc(c.nom)}</td><td>${esc(c.tel)}</td><td>${d0(c.avert)}</td><td>${d0(c.vendable)}</td><td class="r">${fmt(c.total)}</td></tr>`).join('')}</tbody></table>`:empty('✓','Aucun vêtement à vendre','Les commandes averties apparaissent ici après le délai de grâce.')}</div>`;
}
function vNotifs(){
 const tabs=`<div class="tabs"><button data-tab="notifs" class="${S.tab==='notifs'?'on':''}">Notifications</button><button data-tab="mails" class="${S.tab==='mails'?'on':''}">Journal emails</button></div>`;
 if(S.tab==='notifs'){const l=S.d.notifications;
  return tabs+`<div class="card">${l.length?`<div class="list">${l.map(n=>`<div class="note"><span class="dot"></span><div><b>${esc(n.titre)}</b><br>${esc(n.message)}<br><small>${dt(n.date)}</small></div><button class="btn ghost" data-done="${n.id}">Marquer traitée</button></div>`).join('')}</div>`:empty('✓','Aucune notification non traitée','Tout est à jour.')}</div>`}
 const l=S.d.emails;
 return tabs+`<div class="card tw">${l.length?`<table><thead><tr><th>Ticket</th><th>Type</th><th>Destinataire</th><th>Date</th><th>Résultat</th><th></th></tr></thead><tbody>${l.map(e=>`<tr style="cursor:default"><td class="mono">${e.ticket}</td><td>${e.type==='CommandePrete'?'Commande prête':'Avertissement'}${e.manuel?' <span class="badge">manuel</span>':''}</td><td>${esc(e.destinataire)}</td><td>${dt(e.date)}</td><td><span class="badge b-${e.envoye?'ok':'ko'}" title="${esc(e.erreur)}">${e.envoye?'Envoyé':'Échec'}</span></td><td class="r"><button class="btn ghost" data-re="${e.id}">Renvoyer</button></td></tr>`).join('')}</tbody></table>`:empty('✉','Journal vide','Les emails apparaissent ici dès qu\'une commande passe à « Prête » (bouton Synchroniser).')}</div>`;
}
function vLogin(){return `<div class="login"><div class="card lc"><div class="logo big">P</div><h2>PressiGest</h2><p class="muted">Connectez-vous avec votre compte employé</p>${S.err?`<div class="alert">${esc(S.err)}</div>`:''}<label class="f">Identifiant<input id="lg" autocomplete="username" value="${esc(S.lg)}"></label><label class="f">Mot de passe<input id="pw" type="password" autocomplete="current-password"></label><button class="btn" id="login">Se connecter</button></div></div>`}
const vNone=()=>`<div class="card">${empty('◧','Aucun écran pour votre rôle','Ce module gère les retraits, ventes et notifications (caissier et administrateur).')}</div>`;
function render(){
 document.body.classList.toggle('auth',!S.user);
 if(!S.user){$('#view').innerHTML=vLogin();return}
 if(!allowed().includes(S.view))S.view=allowed()[0]||'none';
 const [t,s]=META[S.view]||['PressiGest','Aucun écran disponible pour votre rôle','00'];
 $('#title').textContent=t;$('#sub').textContent=s;
 $('#un').textContent=S.user.nom;$('#ur').textContent=S.user.role;$('#av').textContent=S.user.nom.split(/\s+/).map(w=>w[0]).join('').slice(0,2).toUpperCase();
 $('#status').textContent='● Connecté à la base PostgreSQL'+(S.db?' « '+S.db+' »':'');
 $('#sim').style.display=allowed().length?'':'none';
 $('#view').innerHTML=({retrait:vRetrait,ventes:vVentes,notifs:vNotifs}[S.view]||vNone)();nav();
}
document.addEventListener('click',e=>{
 const g=s=>e.target.closest(s);
 if(g('[data-v]')){S.view=g('[data-v]').dataset.v;return refresh()}
 if(g('[data-tab]')){S.tab=g('[data-tab]').dataset.tab;return refresh()}
 if(g('#go')){S.q=$('#q').value;return refresh()}
 if(g('#rf'))return refresh();
 if(g('tbody tr[data-id]')){S[S.view==='ventes'?'sel2':'sel']=+g('tr').dataset.id;return render()}
 if(g('#pay'))return act(()=>call('payer',{id:S.sel,montant:+$('#amt').value,mode:$('#mode').value}),'Paiement enregistré.');
 if(g('#out'))return act(()=>call('retirer',{id:S.sel}),'Retrait validé.');
 if(g('#sell')){const v=S.d.eligibles.find(x=>x.id===S.sel2);if(!v)return toast('Aucune commande éligible sélectionnée.',true);const p=+$('#prix').value,m=$('#mode').value;
  return ask('Confirmer la vente',`Confirmer la vente du ticket ${v.ticket} pour ${fmt(p)} ?\nLes avances déjà versées ne seront pas remboursées.`,()=>act(()=>call('vendre',{id:v.id,prix:p,mode:m}),'Commande marquée Vendue.'))}
 if(g('[data-done]'))return act(()=>call('traiter',{id:+g('[data-done]').dataset.done}));
 if(g('[data-re]')){const m=S.d.emails.find(x=>x.id==g('[data-re]').dataset.re);return act(()=>call('renvoyer',{ticket:m.ticket,type:m.type}),'Tentative de renvoi enregistrée dans le journal.')}
 if(g('#sim'))return act(()=>call('sync'),'Synchronisation terminée.');
 if(g('#login'))return connexion();
 if(g('#logout'))return call('logout').then(()=>{S.user=null;S.err='';S.lg='';S.q='';S.d={recherche:[],eligibles:[],notifications:[],emails:[]};render()});
});
document.addEventListener('keydown',e=>{if(e.key==='Enter'&&(e.target.id==='pw'||e.target.id==='lg'))return connexion();
 if(e.key==='Enter'&&e.target.id==='q'){S.q=e.target.value;refresh().then(()=>{const i=$('#q');i.focus();i.setSelectionRange(i.value.length,i.value.length)})}});
async function connexion(){
 S.lg=$('#lg').value;
 try{S.user=await call('login',{login:S.lg,motDePasse:$('#pw').value});S.err='';await demarrer()}
 catch(e){S.err=String(e);render()}
}
// Au démarrage : synchronisation réelle (avertissements, notifications « à vendre », emails « prête »), puis toutes les heures
async function demarrer(){S.view='retrait';try{await call('sync')}catch(e){toast(String(e),true)}await refresh()}
(async()=>{
 try{const s=await call('session');S.db=s.base;S.err=s.erreur;S.user=s.user}catch(e){S.err=String(e)}
 S.user?await demarrer():render();
})();
setInterval(()=>S.user&&call('sync').catch(()=>{}).then(refresh),36e5);
