static lv_obj_t *market_columns[3], *market_titles[3], *market_empty[3], *market_scroll_hint;
static lv_obj_t *stock_rows[20], *stock_name[20], *stock_code[20], *stock_price[20], *stock_change[20];
static const char *market_names[]={"美股","港股","A 股"};
static int stock_market(cJSON *quote) {
    const char *symbol=text(quote,"symbol");
    if(!symbol[0]||!symbol[1])return -1;
    char a=symbol[0],b=symbol[1];
    if(a>='A'&&a<='Z')a+='a'-'A';
    if(b>='A'&&b<='Z')b+='a'-'A';
    if(a=='u'&&b=='s')return 0;
    if(a=='h'&&b=='k')return 1;
    if((a=='s'&&(b=='h'||b=='z'))||(a=='b'&&b=='j'))return 2;
    return -1;
}
static UI_INIT_SMALL void markets_create(lv_obj_t *p) {
    stocks=muted(p,"自选行情",4,0,1208);
    for(int m=0;m<3;m++) {
        market_titles[m]=label(p,market_names[m],m*414+12,42,366,false);
        market_columns[m]=lv_obj_create(p);lv_obj_remove_style_all(market_columns[m]);
        lv_obj_set_pos(market_columns[m],m*414,84);lv_obj_set_size(market_columns[m],390,400);
        lv_obj_set_scroll_dir(market_columns[m],LV_DIR_VER);
        lv_obj_set_scrollbar_mode(market_columns[m],LV_SCROLLBAR_MODE_OFF);
        market_empty[m]=muted(market_columns[m],"暂无自选",14,32,360);
    }
    for(int i=0;i<20;i++) {
        lv_obj_t *r=stock_rows[i]=card(market_columns[0],0,i*136,390,128);
        lv_obj_set_style_pad_all(r,12,0);
        stock_name[i]=label(r,"--",2,0,352,false);
        lv_obj_set_height(stock_name[i],32);
        lv_label_set_long_mode(stock_name[i],LV_LABEL_LONG_DOT);
        stock_code[i]=muted(r,"--",2,35,114);
        stock_price[i]=label(r,"--",108,30,246,true);
        lv_obj_set_style_text_align(stock_price[i],LV_TEXT_ALIGN_RIGHT,0);
        stock_change[i]=label(r,"--",176,69,178,false);
        lv_obj_set_style_text_align(stock_change[i],LV_TEXT_ALIGN_RIGHT,0);
        lv_obj_add_flag(r,LV_OBJ_FLAG_HIDDEN);
    }
    market_scroll_hint=muted(p,"列表可上滑查看",12,484,1170);
    visible(market_scroll_hint,false);
}
static void markets_render(void) {
    cJSON *s=item(shown,"stocks"),*quotes=item(s,"quotes");
    int total=cJSON_GetArraySize(quotes),counts[3]={0},unknown=0;
    for(int i=0;i<20;i++) {
        cJSON *q=cJSON_GetArrayItem(quotes,i);int market=q?stock_market(q):-1;
        visible(stock_rows[i],q&&market>=0);
        if(!q)continue;
        if(market<0){unknown++;continue;}
        if(lv_obj_get_parent(stock_rows[i])!=market_columns[market])lv_obj_set_parent(stock_rows[i],market_columns[market]);
        lv_obj_set_pos(stock_rows[i],0,counts[market]++*136);
        ui_text(stock_name[i],text(q,"name"));ui_text(stock_code[i],text(q,"code"));
        ui_text(stock_price[i],text(q,"price"));ui_text(stock_change[i],text(q,"changePercent"));
        char *end;const char *change=text(q,"changePercent");double n=strtod(change,&end);
        uint32_t color=end==change||n==0?MUTED:n>0?RED:GREEN;
        tint(stock_price[i],color);tint(stock_change[i],color);
    }
    bool overflow=false;
    for(int m=0;m<3;m++) {
        ui_text(market_titles[m],market_names[m]);
        visible(market_empty[m],!counts[m]);
        bool column_overflow=counts[m]>3;
        lv_obj_set_scrollbar_mode(market_columns[m],column_overflow?LV_SCROLLBAR_MODE_ON:LV_SCROLLBAR_MODE_OFF);
        overflow|=column_overflow;
    }
    visible(market_scroll_hint,overflow);
    if(unknown)ui_text_fmt(stocks,"%d 项未识别市场，检查电脑自选代码",unknown);
    else ui_text(stocks,!total?"等待电脑行情数据":yes(s,"stale")?"自选行情 · 缓存":"自选行情");
}
