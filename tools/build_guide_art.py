"""Original instructional diagrams; never presented as screenshots."""
from pathlib import Path
from html import escape
OUT=Path(__file__).resolve().parents[1]/'docs/assets/guides'

def diagram(name,title,subtitle,steps):
    h=150+len(steps)*105
    items=[f'<svg xmlns="http://www.w3.org/2000/svg" width="900" height="{h}" viewBox="0 0 900 {h}" role="img"><title>{escape(title)}</title><rect width="900" height="{h}" rx="20" fill="#091b2b"/>']
    def text(x,y,value,size=20,color='#e4f5ff'):
        items.append(f'<text x="{x}" y="{y}" fill="{color}" font-size="{size}" font-family="Segoe UI,Microsoft YaHei,sans-serif">{escape(value)}</text>')
    text(36,49,title,30);text(36,82,subtitle,17,'#98b3c5')
    for i,(heading,detail) in enumerate(steps):
        y=126+i*105
        items.append(f'<circle cx="56" cy="{y+15}" r="23" fill="#153e50" stroke="#78e5ed"/>')
        text(49,y+22,str(i+1),22,'#78e5ed');text(99,y+13,heading,23);text(99,y+47,detail,18,'#b6cbd9')
    text(36,h-19,'AI-bot · 操作示意图 / Workflow diagram · 非操作截图',13,'#8eaabb')
    items.append('</svg>');(OUT/name).write_text(''.join(items)+'\n',encoding='utf-8',newline='\n')
if __name__=='__main__':
    OUT.mkdir(parents=True,exist_ok=True)
    diagram('install.svg','Windows：从源码到桌面小屏','当前没有正式 Release；按手册构建候选包，再完整解压。',[
      ('获取源码与构建环境','Git + Python + .NET 8 SDK；运行本地打包脚本。'),
      ('完整解压 Windows ZIP','保留 DLL、runtimes、许可；安装 Desktop Runtime 与 WebView2。'),
      ('退出旧桥接，再启动 AIBotBridge.exe','右下角托盘运行；左键打开镜像，右键打开功能菜单。'),
      ('连接已刷 AI-bot 固件的设备','使用 USB 数据线；串口自动识别，核实握手与页面切换。'),
      ('按需启用内容','先看系统监控与默认桌宠，再配置天气、股票及厂商授权。')])
    diagram('flash.svg','ESP8266：刷写与成功确认','适用于本仓库 nodemcuv2 / ST7789 SD2 引脚配置。',[
      ('核对硬件与固件版本','240×240 ST7789；屏幕引脚须与 platformio.ini 一致。'),
      ('保留回退材料，释放串口','备份旧固件来源和设置；退出桥接、串口监视器。'),
      ('插入 USB 数据线，查真实 COM 号','设备管理器 → 端口；对比插拔前后新增的 CH340。'),
      ('先构建，再指定串口上传','PlatformIO 负责板卡参数；不要套用 ESP32 地址或擦除全片。'),
      ('重启并启动桥接','上传成功之后，还要看到 USB 握手和设备页面切换。'),
      ('恢复自动与轮播','保留原有页面、顺序、间隔；不要停在最后一个测试页。')])
