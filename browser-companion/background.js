importScripts("config.js");

async function register(origin) {
  const digest=await crypto.subtle.digest("SHA-256",new TextEncoder().encode(origin));
  const id=Array.from(new Uint8Array(digest)).slice(0,8).map(b=>b.toString(16).padStart(2,"0")).join("");
  const scripts=await chrome.scripting.getRegisteredContentScripts();
  if(!scripts.some(s=>s.id==="relay-"+id))await chrome.scripting.registerContentScripts([
    {id:"relay-"+id,matches:[origin],js:["relay.js"],runAt:"document_start",world:"ISOLATED"},
    {id:"capture-"+id,matches:[origin],js:["capture.js"],runAt:"document_idle",world:"MAIN"}
  ]);
}
chrome.runtime.onInstalled.addListener(async()=> {
  const permissions=await chrome.permissions.getAll();
  for(const origin of permissions.origins??[])if(origin.startsWith("https://"))await register(origin);
});
chrome.runtime.onMessage.addListener((message,sender,reply)=> {
  if(message?.kind==="enable"&&!sender.tab) {
    register(message.origin).then(()=>reply({ok:true})).catch(()=>reply({ok:false}));return true;
  }
  if(message?.kind!=="artwork"||!sender.tab||!sender.url?.startsWith("https://")||!AIBOT_MUSIC.key)return;
  (async()=> {
    const origin=new URL(sender.url).origin+"/*";
    if(!await chrome.permissions.contains({origins:[origin]}))return;
    const data=message.data;
    if(typeof data?.title!=="string"||data.title.length>512||typeof data.artist!=="string"||data.artist.length>256)return;
    const body=JSON.stringify(data);if(new TextEncoder().encode(body).length>16384)return;
    try {
      const response=await fetch(AIBOT_MUSIC.endpoint,{method:"POST",headers:{"Content-Type":"application/json","X-AIBot-Music-Key":AIBOT_MUSIC.key},body,signal:AbortSignal.timeout(10000)});
      reply({ok:response.ok});
    }catch{reply({ok:false});}
  })();return true;
});
