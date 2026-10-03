let tab,origin;
chrome.tabs.query({active:true,currentWindow:true}).then(tabs=> {
  tab=tabs[0];if(!tab?.url?.startsWith("https://")){document.getElementById("enable").disabled=true;return;}
  origin=new URL(tab.url).origin+"/*";document.getElementById("status").textContent=new URL(tab.url).hostname;
});
document.getElementById("enable").addEventListener("click",async()=> {
  if(!origin)return;
  const status=document.getElementById("status");
  if(!await chrome.permissions.request({origins:[origin]})){status.textContent="未启用";return;}
  const result=await chrome.runtime.sendMessage({kind:"enable",origin});
  if(!result?.ok){status.textContent="启用失败，请重试。";return;}
  await chrome.scripting.executeScript({target:{tabId:tab.id},files:["relay.js"],world:"ISOLATED"});
  await chrome.scripting.executeScript({target:{tabId:tab.id},files:["capture.js"],world:"MAIN"});
  status.textContent="已启用。换视频后会自动更新封面。";
});
