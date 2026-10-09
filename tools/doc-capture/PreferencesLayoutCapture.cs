using System.Net;
using System.Text;
namespace AIBotBridge;
internal static class PreferencesLayoutCapture
{
    internal static void Run(string output)
    {
        var settings=BridgeSettings.CreatePublicSelfTestSettings();
        if(!settings.SaveEditable(new Dictionary<string,string>{["stock_symbols"]="usIXIC,sh000001,hkHSI,hk02015,sh601939,sz300465,usLI"},out var error))throw new Exception(error);
        StockQuote[] quotes=[new("usIXIC","IXIC","纳斯达克","--","--",0),new("sh000001","000001","上证指数","--","--",0),new("hkHSI","HSI","恒生指数","--","--",0),new("hk02015","02015","理想汽车-W","--","--",0),new("sh601939","601939","建设银行","--","--",0),new("sz300465","300465","高伟达","--","--",0),new("usLI","LI","理想汽车","--","--",0)];
        using var http=new HttpClient(new UpdateSelfTest.Handler{Reply=(req,_)=>{string query=Uri.UnescapeDataString(req.RequestUri!.Query.Split('&')[0][3..]);return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(StockSettingsSelfTest.Fixture(query),Encoding.UTF8)});}});
        var search=new StockSearchService(http);
        using var loop=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new(-30000,-30000),Size=new(1,1)};Exception? failure=null;
        loop.Shown+=async(_,_)=>{try{
            Console.WriteLine("PREFERENCES_CAPTURE_DPI "+loop.DeviceDpi);
            foreach(float scale in new[]{1f,1.5f,2f})foreach(bool minimum in new[]{false,true}){
                using(var stocks=new SettingsForm(settings,false,quotes,search)){
                    Show(stocks,scale,minimum);Save(stocks,$"stocks-{scale:0.0}{(minimum?"-minimum":"")}");
                    ((TextBox)stocks.Controls.Find("stock-code",true).Single()).Text="平安";await stocks.SearchStocksAsync();Application.DoEvents();Save(stocks,$"stocks-search-{scale:0.0}{(minimum?"-minimum":"")}");stocks.Close();
                }
                using(var weather=new MigratedWeather.WeatherSettingsForm(new MigratedWeather.WeatherMonitor())){
                    weather.UsePreviewData();Show(weather,scale,minimum);Save(weather,$"weather-auto-{scale:0.0}{(minimum?"-minimum":"")}");
                    int sourceTop=weather.Controls.Find("weather-source-heading",true).Single().Top;
                    ((RadioButton)weather.Controls.Find("weather-manual-location",true).Single()).Checked=true;((TextBox)weather.Controls.Find("weather-city",true).Single()).Text="南京市雨花台区";
                    Application.DoEvents();if(weather.Controls.Find("weather-source-heading",true).Single().Top!=sourceTop)throw new Exception($"Weather source moved during location switch: {scale} minimum={minimum}, {sourceTop} -> {weather.Controls.Find("weather-source-heading",true).Single().Top}");Save(weather,$"weather-manual-{scale:0.0}{(minimum?"-minimum":"")}");
                    ((RadioButton)weather.Controls.Find("weather-source-free",true).Single()).Checked=true;Application.DoEvents();Save(weather,$"weather-free-{scale:0.0}{(minimum?"-minimum":"")}");weather.Close();
                }
                using(var birthdays=new BirthdaySettingsForm(preview:true)){Show(birthdays,scale,minimum);Save(birthdays,$"birthdays-{scale:0.0}{(minimum?"-minimum":"")}");((ListBox)birthdays.Controls.Find("birthday-list",true).Single()).SelectedIndex=0;Application.DoEvents();Save(birthdays,$"birthdays-edit-{scale:0.0}{(minimum?"-minimum":"")}");birthdays.Close();}
            }
        }catch(Exception ex){failure=ex;}finally{loop.Close();}};Application.Run(loop);if(failure is not null)throw new InvalidOperationException("Preference layout failed",failure);
        Console.WriteLine("PREFERENCES_LAYOUT_OK native stock list/search and automatic/manual weather; default/minimum, 100/150/200 percent; controls/footer bounds checked; isolated fixture only");
        void Show(Form form,float scale,bool minimum){form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new(-30000,-30000);form.Show();Application.DoEvents();if(scale!=1)form.Scale(new SizeF(scale,scale));if(minimum)form.Size=form.MinimumSize;form.PerformLayout();Application.DoEvents();}
        void Save(Form form,string name){
            Check(form);
            foreach(var label in Descendants(form).OfType<Label>().Where(l=>l.Visible))if(label.Height+2<label.GetPreferredSize(new Size(label.Width,0)).Height)throw new Exception("Clipped label "+label.Text);
            var footer=Descendants(form).FirstOrDefault(c=>c.Name=="weather-actions");if(footer is not null&&!form.ClientRectangle.Contains(form.RectangleToClient(footer.RectangleToScreen(footer.ClientRectangle))))throw new Exception("Weather footer outside window");
            using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new(Point.Empty,form.Size));bitmap.Save(Path.Combine(output,name+".png"));
        }
        void Check(Control parent){foreach(Control child in parent.Controls){if(!child.Visible)continue;if(child.Left < -2||child.Right>parent.ClientSize.Width+2)throw new Exception($"Clipped {child.GetType().Name}: {child.Bounds} in {parent.ClientSize}");Check(child);}}
    }
    private static IEnumerable<Control> Descendants(Control parent){foreach(Control child in parent.Controls){yield return child;foreach(var nested in Descendants(child))yield return nested;}}
}
