(() => {
  if(window.__aibotMusicCapture)return;
  window.__aibotMusicCapture=true;
  let signature="",lastSent=0,lastPosition=0,lastMedia=null;
  function capture() {
    const elements=Array.from(document.querySelectorAll("video, audio"));
    const active=elements.filter(m=>!m.ended&&!m.paused&&m.readyState>=3);
    if(active.length>1)return; // A page with simultaneous media has no unambiguous timeline.
    const remaining=elements.filter(m=>!m.ended);
    const media=active[0]||(elements.includes(lastMedia)?lastMedia:remaining.length===1?remaining[0]:elements.length===1?elements[0]:null);
    const metadata=navigator.mediaSession?.metadata;
    if(!media||!metadata?.title)return;
    lastMedia=media;
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
    const album=(metadata.album??"").trim(),playing=!media.paused&&!media.ended&&media.readyState>=3;
    const elapsedSeconds=Number.isFinite(media.currentTime)?Math.max(0,media.currentTime):0;
    const durationSeconds=Number.isFinite(media.duration)?Math.max(0,media.duration):0;
    const playbackRate=Number.isFinite(media.playbackRate)?media.playbackRate:1;
    const data={title,artist,album,youtubeVideoId,artwork,playing,ended:!!media.ended,elapsedSeconds,durationSeconds,playbackRate,sampleTimeMs:Date.now(),source:location.hostname};
    const next=JSON.stringify({title,artist,album,youtubeVideoId,artwork,playing,ended:data.ended,playbackRate});
    const seek=Math.abs(elapsedSeconds-lastPosition-(playing?(Date.now()-lastSent)*playbackRate/1000:0))>2;
    if(next===signature&&!seek&&Date.now()-lastSent<1000)return;
    signature=next;lastSent=Date.now();lastPosition=elapsedSeconds;
    window.dispatchEvent(new CustomEvent("aibot-music-artwork",{detail:JSON.stringify(data)}));
  }
  setInterval(capture,500);capture();
})();
