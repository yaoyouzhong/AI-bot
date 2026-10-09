using System.Net;
using System.Text;
using System.Text.Json;
namespace AIBotBridge;
internal static class StockSettingsSelfTest
{
    internal static readonly StockQuote[] Quotes=[new("sh000001","000001","上证指数","--","--",0),new("sz000001","000001","平安银行","--","--",0),new("hk00700","00700","腾讯控股","--","--",0),new("usAAPL","AAPL","苹果","--","--",0)];
    internal static string Fixture(string query)=>"v_hint="+JsonSerializer.Serialize(query switch {
        "苹果" or "AAPL"=>"us~aapl.oq~苹果~pg~GP^us~brk.b.n~伯克希尔B~bkxeb~GP",
        "腾讯"=>"hk~00700~腾讯控股~txkg~GP^sh~000847~腾讯济安~txja~ZS",
        "平安" or "payh" or "pinganyinhang"=>"sz~000001~平安银行~payh~GP-A",
        _=>"sh~000001~上证指数~szzs~ZS^sz~000001~平安银行~payh~GP-A^jj~000001~华夏成长~hxcz~KJ"});
    internal static void Run(string directory)
    {
        Directory.CreateDirectory(directory);string data=Path.Combine(directory,"settings-"+Guid.NewGuid().ToString("N"));
        var settings=BridgeSettings.CreatePublicSelfTestSettings();if(!settings.SaveEditable(new Dictionary<string,string>(),out var error,data))throw new Exception(error);
        bool fail=false;var held=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler=new UpdateSelfTest.Handler{Reply=async(req,_)=>{string query=Uri.UnescapeDataString(req.RequestUri!.Query.Split('&')[0][3..]);if(query=="old")await held.Task;if(fail)throw new HttpRequestException("fixture offline");return new(HttpStatusCode.OK){Content=new StringContent(Fixture(query),Encoding.UTF8)};}};
        using var http=new HttpClient(handler);var search=new StockSearchService(http);
        using var form=new SettingsForm(settings,false,Quotes,search);form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new(-30000,-30000);
        var input=(TextBox)form.Controls.Find("stock-code",true).Single();var market=(ComboBox)form.Controls.Find("stock-market",true).Single();var results=(ListView)form.Controls.Find("stock-search-results",true).Single();var list=(ListView)form.Controls.Find("stock-list",true).Single();var add=(Button)form.Controls.Find("add-stock",true).Single();
        void Check(bool ok,string message){if(!ok)throw new Exception(message);}
        Exception? failure=null;
        form.Shown+=async(_,_)=>{try{
            input.Text="000001";await form.SearchStocksAsync();Check(results.Items.Count==2,"Code search did not retain both markets or excluded fund incorrectly");Check(!add.Enabled,"Ambiguous search was automatically selected");
            results.Items[1].Selected=true;Application.DoEvents();add.PerformClick();Check(form.StockSymbols.SequenceEqual(new[]{"sh000001","sz000001"}),"Wrong market added for ambiguous code");
            market.SelectedIndex=4;input.Text="腾讯";await form.SearchStocksAsync();Check(results.Items.Count==1&&((StockSearchResult)results.Items[0].Tag!).Symbol=="hk00700","Market filter failed");results.Items[0].Selected=true;Application.DoEvents();add.PerformClick();
            Check(list.Items.Cast<ListViewItem>().Any(i=>i.Text=="腾讯控股"),"Chosen result name not displayed");
            market.SelectedIndex=0;input.Text="old";var old=form.SearchStocksAsync();input.Text="苹果";await form.SearchStocksAsync();held.SetResult();await old;
            Check(results.Items.Count==2&&((StockSearchResult)results.Items[0].Tag!).Symbol=="usAAPL","Old search replaced latest result or US suffix not normalized");
            Check(((StockSearchResult)results.Items[1].Tag!).Symbol=="usBRK.B","Share class suffix lost");
            fail=true;input.Text="失败";await form.SearchStocksAsync();Check(form.StockSymbols.Length==3,"Search failure changed existing selections");fail=false;input.Clear();
            Check(form.TryAddStocks("hk00700，usAAPL\r\nsh000001",0,out error)&&form.StockSymbols.Length==4,"Batch deduplication failed");
            var up=(Button)form.Controls.Find("up-stock",true).Single();var down=(Button)form.Controls.Find("down-stock",true).Single();
            list.Items[3].Selected=true;Application.DoEvents();Check(!down.Enabled,"Last stock could move down");up.PerformClick();Check(form.StockSymbols.SequenceEqual(new[]{"sh000001","sz000001","usAAPL","hk00700"}),"Stock move-up failed");
            up.PerformClick();down.PerformClick();Check(form.StockSymbols.SequenceEqual(new[]{"sh000001","sz000001","usAAPL","hk00700"}),"Repeated move lost selection or order");list.SelectedItems.Clear();list.Items[0].Selected=true;Application.DoEvents();Check(!up.Enabled,"First stock could move up");
            int before=form.StockSymbols.Length;Check(!form.TryAddStocks("usMSFT,非法中文",0,out error)&&form.StockSymbols.Length==before,"Invalid batch partially changed list");
            input.Text="未选中的搜索";Check(!form.TrySave(out error,data),"Unselected query was silently saved");input.Clear();
            var newer=BridgeSettings.LoadCurrentFromDirectory(data);Check(newer.SaveEditable(new Dictionary<string,string>{["weather_city"]="杭州",["weather_latitude"]="30.2",["weather_longitude"]="120.1",["weather_auto_location"]="1",["serial_port"]="COM91"},out error,data),error);
            Check(form.TrySave(out error,data),error);var saved=BridgeSettings.LoadCurrentFromDirectory(data);Check(saved.Get("stock_symbols")=="sh000001,sz000001,usAAPL,hk00700","Saved selection order/code mismatch");Check(saved.Get("weather_city")=="杭州"&&saved.Get("weather_latitude")=="30.2"&&saved.Get("weather_longitude")=="120.1"&&saved.Get("weather_auto_location")=="1"&&saved.Get("serial_port")=="COM91","Stock save overwrote newer weather/device settings");
            using(var reopened=new SettingsForm(saved,false,Quotes,search))Check(reopened.StockSymbols.SequenceEqual(form.StockSymbols),"Saved stock order not restored in reopened editor");
            string quotePayload=string.Join('\n',Quotes.Reverse().Select(q=>{var fields=Enumerable.Repeat("0",33).ToArray();fields[1]=q.Name;fields[3]="10";fields[31]="1";fields[32]="10";return "v_"+StockService.TencentSymbol(q.Symbol)+"=\""+string.Join('~',fields)+"\";";}));
            using(var quoteHttp=new HttpClient(new UpdateSelfTest.Handler{Reply=(_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(Encoding.GetEncoding("GB18030").GetBytes(quotePayload))})})){
                var stockService=new StockService(saved,false,quoteHttp);await stockService.RefreshAsync(CancellationToken.None);
                Check(stockService.Snapshot?.Quotes.Select(q=>q.Symbol).SequenceEqual(form.StockSymbols)==true,"Saved order not applied to bridge stock snapshot");
            }
            for(int i=0;i<16;i++)Check(form.TryAddStocks("sh"+(600100+i),0,out error),error);
            Check(form.StockSymbols.Length==20&&!form.TryAddStocks("usMSFT",0,out error)&&form.StockSymbols.Length==20,"20-stock cap failed");
            list.Items[0].Selected=true;Application.DoEvents();((Button)form.Controls.Find("remove-stock",true).Single()).PerformClick();Check(form.StockSymbols.Length==19&&!form.StockSymbols.Contains("sh000001"),"Selected stock removal failed");
        }catch(Exception ex){failure=ex;}finally{form.Close();}};
        Application.Run(form);if(failure is not null)throw new InvalidOperationException("Stock settings regression failed",failure);
        Console.WriteLine("STOCK_SETTINGS_OK code ambiguity/explicit selection, Chinese search, market filter, stale/cancelled/offline searches, US share-class normalization, batch atomicity/deduplication, 20-item cap, up/down/boundaries/selection, remove/save/reopen order and newer weather/device preservation; fixture transport only");
    }
    internal static async Task LiveAsync(string output)
    {
        var search=new StockSearchService();var checks=new List<object>();
        foreach(var (query,symbol) in new[]{("000001","sz000001"),("平安","sz000001"),("payh","sz000001"),("pinganyinhang","sz000001"),("maotai","sh600519"),("腾讯","hk00700"),("tengxun","hk00700"),("AAPL","usAAPL"),("BRK.B","usBRK.B"),("IXIC","usIXIC")}){
            var results=await search.SearchAsync(query,CancellationToken.None);
            if(!results.Any(r=>r.Symbol==symbol))throw new Exception("Search did not return expected symbol for "+query);
            checks.Add(new{query,expected=symbol,count=results.Length});
        }
        File.WriteAllText(output,JsonSerializer.Serialize(new{passed=true,at=DateTimeOffset.UtcNow,checks},JsonDefaults.Options));
        Console.WriteLine("STOCK_SEARCH_LIVE_OK 10 real queries: codes, Chinese, initials/full pinyin, A/H/US shares and indices; no preferences saved");
    }
}
