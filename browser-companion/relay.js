(() => {
  if(window.__aibotMusicRelay)return;
  window.__aibotMusicRelay=true;
  window.addEventListener("aibot-music-artwork",event=> {
    if(typeof event.detail!=="string"||event.detail.length>16384)return;
    try{chrome.runtime.sendMessage({kind:"artwork",data:JSON.parse(event.detail)}).catch(()=>{});}catch{}
  });
})();
