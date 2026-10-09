using System.Globalization;

namespace AIBotBridge;

internal sealed class SettingsForm : Form
{
    private readonly BridgeSettings _settings;
    private readonly bool _includeDeviceSettings;
    private readonly TextBox _stockInput = new() { Name="stock-code", Dock=DockStyle.Fill, PlaceholderText="代码、中文名称或拼音" };
    private readonly ComboBox _market = new() { Name="stock-market", DropDownStyle=ComboBoxStyle.DropDownList, Width=100 };
    private readonly ListView _stocks = new() { Name="stock-list", Dock=DockStyle.Fill, View=View.Details, FullRowSelect=true, HideSelection=false, MultiSelect=false, BorderStyle=BorderStyle.FixedSingle };
    private readonly Label _stockStatus = new() { AutoSize=true, Dock=DockStyle.Fill };
    private readonly Label _stockCount = new() { AutoSize=true, Margin=new(14,7,0,0) };
    private readonly IReadOnlyList<StockQuote> _quotes;
    private readonly StockSearchService _search;
    private readonly System.Windows.Forms.Timer _searchTimer=new(){Interval=350};
    private CancellationTokenSource? _searchStop;
    private int _searchGeneration;
    private bool _disposed;
    private readonly ListView _suggestions=new(){Name="stock-search-results",View=View.Details,FullRowSelect=true,MultiSelect=false,HideSelection=false,Dock=DockStyle.Fill,Height=110};
    private readonly TableLayoutPanel _suggestionPane=new(){AutoSize=true,Dock=DockStyle.Fill,ColumnCount=1,Visible=false,Margin=new(0,0,0,14)};
    private readonly Dictionary<string,string> _names=new(StringComparer.OrdinalIgnoreCase);
    private readonly NumericUpDown _screenSaver = new() { Minimum = 0, Maximum = 1440, Width = 100 };
    private readonly TextBox _serialPort = new() { Width = 120, CharacterCasing = CharacterCasing.Upper };

    internal SettingsForm(BridgeSettings? settings=null,bool includeDeviceSettings=true,IReadOnlyList<StockQuote>? quotes=null,StockSearchService? search=null)
    {
        SuspendLayout();
        // Reopened editors must see restores and edits made by other windows.
        _settings = settings ?? BridgeSettings.Load();settings=_settings;_includeDeviceSettings=includeDeviceSettings;
        _quotes=quotes??[];
        _search=search??new();
        Text = includeDeviceSettings ? "AI-bot · 股票与设备设置" : "AI-bot · 自选股票";
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Microsoft YaHei UI", 9.5F);
        BackColor=Color.White;ForeColor=Color.FromArgb(32,43,59);
        MinimumSize = new Size(620, includeDeviceSettings ? 720 : 610);
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(720, includeDeviceSettings ? 760 : 620);

        _market.Items.AddRange(["全部市场","沪市","深市","北交所","港股","美股"]);_market.SelectedIndex=0;
        _stocks.Columns.Add("名称",240);_stocks.Columns.Add("市场",100);_stocks.Columns.Add("代码",140);
        foreach(string raw in settings.GetList("stock_symbols","sh000001").Distinct(StringComparer.OrdinalIgnoreCase)){string symbol=StockService.Normalize(raw);AddStock(symbol.Length>0?symbol:raw);}
        _stocks.SizeChanged+=(_,_)=>{int width=Math.Max(200,_stocks.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-4);_stocks.Columns[0].Width=Math.Max(100,width-_stocks.Columns[1].Width-_stocks.Columns[2].Width);};
        if (int.TryParse(settings.Get("screensaver_timeout_minutes"), out var minutes))
            _screenSaver.Value = Math.Clamp(minutes, 0, 1440);
        _serialPort.Text = settings.Get("serial_port");

        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=8,Padding=new(24,24,24,8),Margin=Padding.Empty};layout.ColumnStyles.Add(new(SizeType.Percent,100));
        for(int i=0;i<8;i++)layout.RowStyles.Add(new(i==5?SizeType.Percent:SizeType.AutoSize,i==5?100:0));
        layout.Controls.Add(new Label{Text="自选股票",AutoSize=true,Font=new(Font.FontFamily,17,FontStyle.Bold),Margin=new(0,0,0,8)},0,0);
        layout.Controls.Add(new Label{Text="添加关注的股票与指数，所有已添加设备共用此列表。",AutoSize=true,ForeColor=Color.FromArgb(88,101,119),Margin=new(0,0,0,20)},0,1);
        var entry=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,ColumnCount=3,Margin=new(0,0,0,8)};entry.ColumnStyles.Add(new(SizeType.AutoSize));entry.ColumnStyles.Add(new(SizeType.Percent,100));entry.ColumnStyles.Add(new(SizeType.AutoSize));
        var add=new Button{Text="搜索",Name="search-stock",Margin=new(12,0,0,0)};SettingsWindow.StyleButton(add);
        _market.Margin=new(0,2,10,0);_stockInput.Margin=new(0,2,0,0);entry.Controls.Add(_market,0,0);entry.Controls.Add(_stockInput,1,0);entry.Controls.Add(add,2,0);layout.Controls.Add(entry,0,2);
        _stockStatus.ForeColor=Color.FromArgb(88,101,119);_stockStatus.Margin=new(0,0,0,16);_stockStatus.Text="输入后从搜索结果选择，再加入自选。\n例如：000001、平安、payh / pinganyinhang。";layout.Controls.Add(_stockStatus,0,3);
        _suggestionPane.ColumnStyles.Add(new(SizeType.Percent,100));_suggestions.Columns.Add("名称",240);_suggestions.Columns.Add("市场",100);_suggestions.Columns.Add("代码",140);_suggestions.Margin=Padding.Empty;_suggestionPane.Controls.Add(_suggestions,0,0);
        var pick=new Button{Text="加入自选",Name="add-stock",Enabled=false,Margin=new(0,6,0,0)};SettingsWindow.StyleButton(pick);
        _suggestionPane.Controls.Add(pick,0,1);layout.Controls.Add(_suggestionPane,0,4);
        _suggestions.SizeChanged+=(_,_)=>_suggestions.Columns[0].Width=Math.Max(100,_suggestions.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-4-_suggestions.Columns[1].Width-_suggestions.Columns[2].Width);
        _suggestions.SelectedIndexChanged+=(_,_)=>pick.Enabled=_suggestions.SelectedItems.Count>0;
        void Pick(){if(_suggestions.SelectedItems.Count==0)return;var result=(StockSearchResult)_suggestions.SelectedItems[0].Tag!;if(!TryAddStocks(result.Symbol,0,out var error)){_stockStatus.Text=error;return;}_names[result.Symbol]=result.Name;foreach(ListViewItem item in _stocks.Items)if((string)item.Tag! ==result.Symbol)item.Text=result.Name;_stockInput.Clear();_stockStatus.Text="已加入自选，点击“保存”后生效。";}
        pick.Click+=(_,_)=>Pick();_suggestions.DoubleClick+=(_,_)=>Pick();_suggestions.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;Pick();}};
        var selectedStocks=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty};selectedStocks.ColumnStyles.Add(new(SizeType.Percent,100));selectedStocks.RowStyles.Add(new(SizeType.AutoSize));selectedStocks.RowStyles.Add(new(SizeType.Percent,100));
        _stockCount.Margin=new(0,0,0,8);_stockCount.Font=new(Font,FontStyle.Bold);selectedStocks.Controls.Add(_stockCount,0,0);_stocks.Margin=Padding.Empty;selectedStocks.Controls.Add(_stocks,0,1);layout.Controls.Add(selectedStocks,0,5);
        var listActions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,Margin=new(0,10,0,12)};
        var remove=new Button{Text="移除所选",Name="remove-stock",Enabled=false,Margin=Padding.Empty};SettingsWindow.StyleButton(remove);
        var up=new Button{Text="上移",Name="up-stock",Enabled=false,Margin=new(10,0,0,0)};var down=new Button{Text="下移",Name="down-stock",Enabled=false,Margin=new(10,0,0,0)};SettingsWindow.StyleButton(up);SettingsWindow.StyleButton(down);
        var batch=new LinkLabel{Text="批量添加代码",AutoSize=true,Margin=new(16,7,0,0)};batch.LinkClicked+=(_,_)=>ShowBatch();
        listActions.Controls.AddRange([remove,up,down,batch]);layout.Controls.Add(listActions,0,6);
        void QueueSearch(){if(_disposed)return;_searchGeneration++;_searchStop?.Cancel();_searchTimer.Stop();_suggestionPane.Visible=false;if(!string.IsNullOrWhiteSpace(_stockInput.Text))_searchTimer.Start();}
        _stockInput.TextChanged+=(_,_)=>QueueSearch();_market.SelectedIndexChanged+=(_,_)=>QueueSearch();_searchTimer.Tick+=async(_,_)=>{_searchTimer.Stop();await SearchStocksAsync();};
        add.Click+=async(_,_)=>{_searchTimer.Stop();await SearchStocksAsync();};_stockInput.KeyDown+=async(_,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;_searchTimer.Stop();await SearchStocksAsync();}else if(e.KeyCode==Keys.Down&&_suggestions.Items.Count>0&&_suggestionPane.Visible){e.SuppressKeyPress=true;_suggestions.Focus();_suggestions.Items[0].Selected=true;}};
        void SelectionActions(){int index=_stocks.SelectedIndices.Count>0?_stocks.SelectedIndices[0]:-1;remove.Enabled=index>=0;up.Enabled=index>0;down.Enabled=index>=0&&index<_stocks.Items.Count-1;}
        _stocks.SelectedIndexChanged+=(_,_)=>SelectionActions();
        void Move(int direction){if(_stocks.SelectedIndices.Count==0)return;int index=_stocks.SelectedIndices[0],next=index+direction;if(next<0||next>=_stocks.Items.Count)return;var item=_stocks.Items[index];_stocks.BeginUpdate();try{_stocks.Items.RemoveAt(index);_stocks.Items.Insert(next,item);item.Selected=true;item.Focused=true;item.EnsureVisible();}finally{_stocks.EndUpdate();}SelectionActions();}
        up.Click+=(_,_)=>Move(-1);down.Click+=(_,_)=>Move(1);
        remove.Click+=(_,_)=>{if(_stocks.SelectedItems.Count>0)_stocks.Items.Remove(_stocks.SelectedItems[0]);UpdateCount();};
        layout.SizeChanged+=(_,_)=>_stockStatus.MaximumSize=new(Math.Max(100,layout.ClientSize.Width-layout.Padding.Horizontal),0);
        if(includeDeviceSettings){
            var device=new TableLayoutPanel{AutoSize=true,ColumnCount=2,Margin=new(0,0,0,12)};device.ColumnStyles.Add(new(SizeType.AutoSize));device.ColumnStyles.Add(new(SizeType.Percent,100));
            device.Dock=DockStyle.Fill;AddRow(device,0,"屏保等待（分钟）",_screenSaver);AddRow(device,1,"串口（空白为自动）",_serialPort);
            var note=new Label{Text="屏保 0 为关闭；串口更改后停用并启用设备生效。",AutoSize=true,ForeColor=Color.FromArgb(88,101,119),Dock=DockStyle.Fill};device.Controls.Add(note,0,2);device.SetColumnSpan(note,2);
            device.SizeChanged+=(_,_)=>note.MaximumSize=new(Math.Max(100,device.ClientSize.Width),0);layout.Controls.Add(device,0,7);
        }

        var save = new Button { Text = "保存", Name="save", AutoSize = true };
        var cancel = new Button { Text = "取消", AutoSize = true, DialogResult = DialogResult.Cancel };
        save.Click += (_, _) => SaveAndClose();
        var buttons = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        buttons.Controls.Add(save);
        buttons.Controls.Add(cancel);
        var root = new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=1, RowCount=2 };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        buttons.Dock=DockStyle.Fill;buttons.Padding=new Padding(24,8,24,20);buttons.Margin=Padding.Empty;
        SettingsWindow.StyleButton(save,true);SettingsWindow.StyleButton(cancel);
        root.Controls.Add(layout,0,0);root.Controls.Add(buttons,0,1);Controls.Add(root);
        AcceptButton = save;
        CancelButton = cancel;
        cancel.CausesValidation=false;
        cancel.Click+=(_,_)=>Close();
        SettingsWindow.FitScreen(this);
        ResumeLayout(true);
    }

    internal void FocusSection(string? section)
    {
        if (section == "stocks") { _stockInput.Focus(); }
    }

    private static void AddRow(TableLayoutPanel layout, int row, string label, Control control)
    {
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(3, 8, 3, 8);
        layout.Controls.Add(control, 1, row);
    }

    private void SaveAndClose()
    {
        if(!TrySave(out var error)){Warn(error);return;}
        MessageBox.Show(this,"设置已保存。", "AI-bot",MessageBoxButtons.OK, MessageBoxIcon.Information);
        DialogResult = DialogResult.OK;Close();
    }

    internal bool TrySave(out string error,string? directoryOverride=null)
    {
        if(!string.IsNullOrWhiteSpace(_stockInput.Text)){
            error="请先从搜索结果选择并加入自选，或清空搜索框后保存现有列表。";return false;
        }
        var symbols = StockSymbols;
        if (symbols.Length > 20 || symbols.Any(s=>!StockSearchService.ValidSymbol(s)))
        {
            error="股票代码最多 20 个，只支持 sh、sz、bj、hk、us 市场前缀；A 股可直接填写 6 位代码。";
            return false;
        }

        var port = _serialPort.Text.Trim().ToUpperInvariant();
        if (_includeDeviceSettings && port.Length > 0 && !(port.Length > 3 && port.StartsWith("COM", StringComparison.Ordinal) &&
                               port[3..].All(char.IsDigit)))
        {
            error="串口必须留空或填写 COM 后跟数字，例如 COM7。";
            return false;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["stock_symbols"] = string.Join(',', symbols.Distinct(StringComparer.OrdinalIgnoreCase)),
            ["screensaver_timeout_minutes"] = decimal.ToInt32(_screenSaver.Value).ToString(CultureInfo.InvariantCulture),
            ["serial_port"] = port
        };
        if(!_includeDeviceSettings){values.Remove("screensaver_timeout_minutes");values.Remove("serial_port");}
        return _settings.SaveEditable(values,out error,directoryOverride);
    }

    internal string[] StockSymbols=>_stocks.Items.Cast<ListViewItem>().Select(item=>(string)item.Tag!).ToArray();
    internal bool TryAddStocks(string input,int marketIndex,out string error)
    {
        string[] prefixes=["","sh","sz","bj","hk","us"];
        var raw=input.Replace('，',',').Replace('\r',',').Replace('\n',',').Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        var symbols=new List<string>();
        foreach(string item in raw){
            string candidate=item.Trim();bool prefixed=candidate.Length>2&&prefixes.Skip(1).Contains(candidate[..2],StringComparer.OrdinalIgnoreCase);
            if(!prefixed){
                if(marketIndex>0&&marketIndex<prefixes.Length)candidate=prefixes[marketIndex]+candidate;
                else if(candidate.Length==5&&candidate.All(char.IsDigit))candidate="hk"+candidate;
                else if(candidate.Any(char.IsLetter)&&candidate.All(c=>char.IsAsciiLetterOrDigit(c)||c is '.' or '^' or '-'))candidate="us"+candidate;
            }
            string symbol=StockService.Normalize(candidate);
            if(!StockSearchService.ValidSymbol(symbol)){error=$"无法识别“{item}”。请选择市场并填写股票或指数代码，例如沪市 600519、港股 00700、美股 AAPL。";return false;}
            symbols.Add(symbol);
        }
        if(symbols.Count==0){error="请先输入要添加的股票或指数代码。";return false;}
        var additions=symbols.Distinct(StringComparer.OrdinalIgnoreCase).Except(StockSymbols,StringComparer.OrdinalIgnoreCase).ToArray();
        if(StockSymbols.Length+additions.Length>20){error="最多添加 20 个，请先移除不再关注的项目。";return false;}
        foreach(string symbol in additions)AddStock(symbol);
        error=string.Empty;return true;
    }
    private void AddStock(string symbol)
    {
        string market=symbol.Length>2?symbol[..2] switch{"sh"=>"沪市","sz"=>"深市","bj"=>"北交所","hk"=>"港股","us"=>"美股",_=>"待确认"}:"待确认";
        string name=_names.GetValueOrDefault(symbol)??_quotes.FirstOrDefault(q=>q.Symbol.Equals(symbol,StringComparison.OrdinalIgnoreCase))?.Name??"待获取名称";
        _stocks.Items.Add(new ListViewItem([name,market,StockService.DisplayCode(symbol)]){Tag=symbol});UpdateCount();
    }
    private void UpdateCount()=>_stockCount.Text=$"我的自选 · {_stocks.Items.Count} / 20";

    internal async Task SearchStocksAsync()
    {
        if(_disposed)return;
        _searchTimer.Stop();string query=_stockInput.Text.Trim();if(query.Length==0)return;
        _searchStop?.Cancel();_searchStop?.Dispose();var stop=_searchStop=new();int generation=++_searchGeneration;int market=_market.SelectedIndex;
        _stockStatus.ForeColor=Color.FromArgb(88,101,119);_stockStatus.Text="正在搜索…";
        try{
            var results=await _search.SearchAsync(query,stop.Token);
            if(_disposed||IsDisposed||stop.IsCancellationRequested||generation!=_searchGeneration)return;
            string[] prefixes=["","sh","sz","bj","hk","us"];
            if(market>0)results=results.Where(r=>r.Symbol.StartsWith(prefixes[market],StringComparison.Ordinal)).ToArray();
            _suggestions.Items.Clear();foreach(var result in results)_suggestions.Items.Add(new ListViewItem([result.Name,result.Market,StockService.DisplayCode(result.Symbol)]){Tag=result});
            int rowHeight=_suggestions.Font.Height+(int)Math.Ceiling(6*_suggestions.DeviceDpi/96d);
            _suggestions.Height=rowHeight*(Math.Clamp(results.Length,1,4)+1)+8;
            _suggestionPane.Visible=results.Length>0;
            _stockStatus.Text=results.Length>0?$"找到 {results.Length} 项，请选中后点击“加入自选”，也可双击结果。":"没有找到匹配项，请换个关键词或选择“全部市场”。";
        }catch(OperationCanceledException) when(stop.IsCancellationRequested){}
        catch(Exception ex) when(ex is HttpRequestException or IOException or System.Text.Json.JsonException or ArgumentException or OperationCanceledException){if(!_disposed&&!IsDisposed&&generation==_searchGeneration){_suggestionPane.Visible=false;_stockStatus.ForeColor=Color.Firebrick;_stockStatus.Text="搜索暂不可用，原自选列表保留。";}}
    }
    private void ShowBatch()
    {
        using var form=new Form{Text="批量添加股票代码",Font=Font,ClientSize=new(480,270),StartPosition=FormStartPosition.CenterParent};
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new(16),ColumnCount=1,RowCount=3};root.ColumnStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.AutoSize));root.RowStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.AutoSize));
        var hint=new Label{Text="使用带市场前缀的代码，用逗号或换行分隔。\n例如：sh000001、hk00700、usAAPL。",AutoSize=true,Dock=DockStyle.Fill};var input=new TextBox{Multiline=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical};var add=new Button{Text="添加到列表"};SettingsWindow.StyleButton(add,true);
        root.Controls.Add(hint,0,0);root.Controls.Add(input,0,1);root.Controls.Add(add,0,2);form.Controls.Add(root);
        add.Click+=(_,_)=>{if(!TryAddStocks(input.Text,_market.SelectedIndex,out var error)){hint.Text=error;return;}form.Close();};SettingsWindow.FitScreen(form);form.ShowDialog(this);
    }
    protected override void Dispose(bool disposing){if(disposing&&!_disposed){_disposed=true;_searchTimer.Dispose();_searchStop?.Cancel();_searchStop?.Dispose();_searchStop=null;}base.Dispose(disposing);}

    private static void Warn(string message) => MessageBox.Show(message, "AI-bot 设置",
        MessageBoxButtons.OK, MessageBoxIcon.Warning);
}
