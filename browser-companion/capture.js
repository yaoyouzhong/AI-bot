(() => {
  if(window.__aibotMusicCapture)return;
  window.__aibotMusicCapture=true;
  let signature="",lastSent=0;
  function capture() {
    const media=document.querySelector("video, audio");
    const metadata=navigator.mediaSession?.metadata;
    if(!media||media.ended||!metadata?.title)return;
    const title=metadata.title.trim(),artist=(metadata.artist??"").trim();
    const artwork=Array.from(metadata.artwork??[]).sort((a,b)=> {
      const size=item=>Math.max(0,...(item.sizes??"").split(/\s+/).map(s=>parseInt(s,10)||0));return size(b)-size(a);
    }).map(item=>item.src).filter(src=>typeof src==="string"&&src.startsWith("https://")).slice(0,8);
    let youtubeVideoId="";
    if(["www.youtube.com","m.youtube.com","youtube.com"].includes(location.hostname)&&location.pathname==="/watch") {
      const visibleTitle=document.querySelector("ytd-watch-metadata h1 yt-formatted-string")?.textContent?.trim();
      if(visibleTitle&&visibleTitle!==title)return; // Navigation can briefly retain the old Media Session.
      const id=new URL(location.href).searchParams.get("v");if(/^[\w-]{11}$/.test(id??""))youtubeVideoId=id;
    }
    if(!artwork.length&&!youtubeVideoId&&media.poster?.startsWith("https://"))artwork.push(media.poster);
    const data={title,artist,youtubeVideoId,artwork};const next=JSON.stringify(data);
    if(next===signature&&Date.now()-lastSent<15000)return;
    signature=next;lastSent=Date.now();
    window.dispatchEvent(new CustomEvent("aibot-music-artwork",{detail:next}));
  }
  setInterval(capture,500);capture();
})();
