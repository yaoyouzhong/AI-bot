using System.Text.Json;
using Settings = AIBotBridge.MigratedWeather.Settings;

namespace AIBotBridge.MigratedDomestic;

sealed partial class DomesticQuotaService
{
    internal string Product(string provider)
    {
        var products = CodingPlanSources.Products(provider);
        if (products.Length == 0) return "";
        var saved = Settings.Get("domestic_product_" + provider);
        if (products.Any(p => p.Id == saved)) return saved;
        // Existing users retain their selected statistical source. New providers
        // open the subscription first; old caches are never renamed as plans.
        var s = Snapshot;
        return provider switch
        {
            "zhipu" when s.Zhipu?.Balance != null => "wallet",
            "qwen" when s.QwenFetchedAt.HasValue => "token",
            "stepfun" when s.StepFun != null => "wallet",
            "baidu" when s.Baidu != null => "package",
            _ => products[0].Id
        };
    }

    internal bool SelectedIsPlan(string provider) => Product(provider) is { Length: > 0 } p && CodingPlanSources.IsPlan(p);
    internal bool SupportsSelectedApi(string provider) => SupportsOfficialApi(provider)
        && (provider != "zhipu" || Product(provider) == "coding")
        && (provider != "stepfun" || Product(provider) == "wallet")
        && (provider != "baidu" || Product(provider) == "package");

    internal DomesticProviderQuotaSnapshot? Plan(string provider)
    {
        if (!SelectedIsPlan(provider)) return null;
        var snapshot = Snapshot;
        var value = snapshot.Plans.GetValueOrDefault(CodingPlanSources.Key(provider, Product(provider)));
        if (value != null) return value;
        var legacy = LegacyDisplayCache.ParseDomestic(JsonSerializer.SerializeToElement(snapshot, new JsonSerializerOptions { IncludeFields = true }));
        return provider switch
        {
            "qwen" when Product(provider) == "token" => legacy.Alibaba,
            "kimi" => legacy.Kimi,
            "minimax" => legacy.MiniMax,
            "xiaomi" => XiaomiQuota.Snapshot(snapshot.XiaomiPlanPct, snapshot.XiaomiFetchedAt, snapshot.XiaomiPlanResetAt, snapshot.XiaomiPlanWindowSeconds),
            _ => null
        };
    }

    internal void SelectProduct(string provider, string product)
    {
        if (!CodingPlanSources.Products(provider).Any(p => p.Id == product)) throw new ArgumentException("Unknown plan product.");
        Settings.Set("domestic_product_" + provider, product);
        RefreshHealth.Recover(provider);
        lock (_lock) { _backoffUntil.Remove(provider); _lastRefreshAttempt.Remove(provider); }
    }

    internal void SetPlan(string provider, string product, DomesticProviderQuotaSnapshot value)
    {
        if (!CodingPlanSources.Products(provider).Any(p => p.Id == product && CodingPlanSources.IsPlan(p.Id))
            || value.Provider != (provider == "qwen" ? "alibaba" : provider) || value.Balance.HasValue
            || value.PrimaryPercent is null && value.WeeklyPercent is null && value.PlanPercent is null)
            throw new JsonException("Invalid subscription display.");
        foreach (var pct in new[] { value.PrimaryPercent, value.WeeklyPercent, value.PlanPercent })
            if (pct.HasValue && (!double.IsFinite(pct.Value) || pct < 0 || pct > 100)) throw new JsonException("Invalid subscription percent.");
        lock (_lock) { _snapshot.Plans[CodingPlanSources.Key(provider, product)] = value; Save(); }
        RefreshHealth.Recover(provider);
    }

    internal bool ApplyPlanResponse(string provider, string product, string json)
    {
        if (Product(provider) != product) return false;
        using var document = JsonDocument.Parse(json);
        var value = provider switch
        {
            "zhipu" when product == "coding" => QuotaSourceApi.Parse("zhipu", document.RootElement),
            "qwen" when product == "coding" => CodingPlanSources.ParseQwen(document.RootElement),
            _ => throw new ArgumentException("No JSON plan adapter for this product.")
        };
        if (Product(provider) != product) return false;
        SetPlan(provider, product, value);
        return true;
    }
}

sealed partial class DomesticQuotaAuthForm
{
    internal static void CodingPlanPreview(string directory)
    {
        Directory.CreateDirectory(directory);
        using var service = new PreviewService();
        service.Value.SelectProduct("zhipu", "coding"); service.Value.ApplyPlanResponse("zhipu", "coding", CodingPlanSelfTest.Glm);
        service.Value.SelectProduct("qwen", "coding"); service.Value.ApplyPlanResponse("qwen", "coding", CodingPlanSelfTest.Qwen);
        foreach (var provider in new[] { "zhipu", "qwen", "minimax", "xiaomi", "tencent", "huawei" })
        foreach (var scale in new[] { 1f, 1.25f, 1.75f })
        {
            using var form = new DomesticQuotaAuthForm(service.Value, hideOnUserClose: false, initialProviderId: provider, initializeBrowser: false);
            form.Size = scale == 1 ? new Size(900, 620) : new Size(1060, 820);
            form._backgroundRefresh = true; form.ShowInTaskbar = false; form.Opacity = 0;
            form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-30000, -30000);
            form.Show(); Application.DoEvents();
            form.ConfigureProviderDisplay(ProviderById(provider));
            form.Scale(new SizeF(scale, scale)); form.PerformLayout();
            var choice = form._productChoice.Bounds;
            var productBounds = new Rectangle(form.PointToClient(form._productChoice.PointToScreen(Point.Empty)), form._productChoice.Size);
            var summaryBounds = new Rectangle(form.PointToClient(form._planSummary.PointToScreen(Point.Empty)), form._planSummary.Size);
            if (choice.Width < 200 || form._productChoice.Items.Count == 0 || !form.ClientRectangle.Contains(productBounds)
                || !form.ClientRectangle.Contains(summaryBounds)) throw new InvalidOperationException("Product selector or summary clipped");
            using var image = new Bitmap(form.Width, form.Height);
            form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
            image.Save(Path.Combine(directory, $"{provider}-{scale:0.00}.png"));
            form.Hide();
        }
        var status = SessionActivityReader.Capture() with { DomesticQuotas = new AIBotBridge.DomesticQuotaSnapshot(null, null, null, null, Zhipu: service.Value.Plan("zhipu")) };
        using var mirror = new Bitmap(240, 240);
        using (var g = Graphics.FromImage(mirror)) DomesticPageRenderer.Draw(g, status, "domestic_zhipu");
        mirror.Save(Path.Combine(directory, "glm-plan-mirror.png"));
        Console.WriteLine("CODING_PLAN_UI_PREVIEW_OK offscreen 100/125/175 percent; six providers and GLM mirror; no live accounts");
    }

    private sealed class PreviewService : IDisposable
    {
        internal DomesticQuotaService Value { get; } = new();
        public void Dispose() => Value.DisposeBrowser();
    }

    readonly ComboBox _productChoice = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260, AccessibleName = "统计内容" };
    readonly Label _planSummary = new() { AutoSize = true, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(51, 65, 85), Margin = new Padding(0, 8, 0, 0) };
    FlowLayoutPanel? _productRow;
    int _planCaptureGeneration = -1;
    bool _configuringProduct;

    void InitializePlanControls(TableLayoutPanel header)
    {
        _productRow = new() { AutoSize = true, Dock = DockStyle.Fill, WrapContents = true, Margin = new Padding(0, 8, 0, 0) };
        _productRow.Controls.Add(new Label { Text = "统计内容", AutoSize = true, Margin = new Padding(0, 5, 12, 0) });
        _productRow.Controls.Add(_productChoice);
        header.Controls.Add(_productRow, 0, 3); header.SetColumnSpan(_productRow, 2);
        header.Controls.Add(_planSummary, 0, 4); header.SetColumnSpan(_planSummary, 2);
        _productChoice.SelectionChangeCommitted += async (_, _) =>
        {
            if (_configuringProduct || _activeProvider is null || _productChoice.SelectedItem is not CodingPlanSources.Product product) return;
            _service.SelectProduct(_activeProvider.Id, product.Id);
            await SelectProvider(ProviderById(_activeProvider.Id));
        };
    }

    DomesticProviderDefinition PlanProvider(DomesticProviderDefinition provider)
    {
        var product = CodingPlanSources.Products(provider.Id).FirstOrDefault(p => p.Id == _service.Product(provider.Id));
        return product is null ? provider : provider with { Url = product.Url };
    }

    void ConfigurePlanControls(DomesticProviderDefinition provider)
    {
        _configuringProduct = true;
        try
        {
            var products = CodingPlanSources.Products(provider.Id);
            _productChoice.Items.Clear(); _productChoice.Items.AddRange(products);
            _productChoice.SelectedItem = products.FirstOrDefault(p => p.Id == _service.Product(provider.Id));
            if (_productRow != null) _productRow.Visible = products.Length > 0;
            _miniMaxKeyPanel.Visible = _service.SupportsSelectedApi(provider.Id);
            if (provider.Id == "zhipu" && _service.SelectedIsPlan(provider.Id))
            {
                _miniMaxApiKey.PlaceholderText = "智谱个人版 Coding Plan API Key；留空测试已保存 Key";
                _providerState.Text = "个人版套餐可保存 Key 查询；团队版请登录对应的用量页。";
            }
            else if (_service.SelectedIsPlan(provider.Id) && provider.Id is not ("minimax" or "kimi"))
                _providerState.Text = "登录官方套餐用量页读取已用比例；真实订阅账号待验证。";
            UpdatePlanSummary();
        }
        finally { _configuringProduct = false; }
    }

    void UpdatePlanSummary()
    {
        if (_activeProvider is null) return;
        _planSummary.Visible = _service.SelectedIsPlan(_activeProvider.Id);
        if (!_planSummary.Visible) return;
        var plan = _service.Plan(_activeProvider.Id);
        _planSummary.Text = CodingPlanPage.Summary(plan)
            + (plan != null && (_service.RefreshHealth.Failed(_activeProvider.Id) || DateTimeOffset.UtcNow - plan.UpdatedAt > TimeSpan.FromMinutes(6)) ? " 缓存数据，等待更新。" : "");
    }

    async Task CapturePlanPage(int generation)
    {
        if (_planCaptureGeneration == generation) return;
        _planCaptureGeneration = generation;
        var provider = _activeProvider?.Id;
        if (provider is null || !_service.SelectedIsPlan(provider)) return;
        var product = _service.Product(provider);
        // JSON-backed adapters are authoritative for these contracts. DOM is for
        // providers without a verified query contract, not a fallback on API errors.
        if (provider is "qwen" or "zhipu" or "minimax" or "kimi" or "xiaomi") return;
        for (int attempt = 0; attempt < 20; attempt++)
        {
            await Task.Delay(2500);
            if (IsDisposed || generation != _navigationGeneration || _activeProvider?.Id != provider
                || _service.Product(provider) != product || _capturedForNavigation) return;
            if (_web.CoreWebView2 is not { } core || !CodingPlanPage.Allows(provider, product, core.Source)) continue;
            try
            {
                var result = await core.ExecuteScriptAsync(CodingPlanPage.Script);
                if (generation != _navigationGeneration || _service.Product(provider) != product || _capturedForNavigation) return;
                using var json = JsonDocument.Parse(result);
                var value = CodingPlanPage.Parse(provider, product, json.RootElement);
                _service.SetPlan(provider, product, value);
                _capturedForNavigation = true;
                UpdatePlanSummary();
                _status.Text = "已取得 " + value.Plan + " 套餐用量并缓存。";
                await PersistLoginCookies(_activeProvider.Url);
                if (_backgroundRefresh) Hide();
                return;
            }
            catch (JsonException) { /* No matching labelled progress yet. Keep last-good data. */ }
            catch (Exception e) when (e is InvalidOperationException or System.Runtime.InteropServices.COMException) { return; }
        }
        if (generation == _navigationGeneration && !IsDisposed)
        {
            _service.ReportWebFailure(provider, "尚未识别套餐用量；请登录并打开已订阅套餐的用量页。已保留缓存。", !_backgroundRefresh);
            _status.Text = "尚未识别套餐用量；请打开套餐用量页，点击刷新额度重试。真实账号适配待验证。";
            UpdatePlanSummary();
        }
    }
}
