"""Install narrow optional cache hooks; fail on an unknown LVGL source layout."""
from pathlib import Path
import sys

root=Path(sys.argv[1])/'managed_components/lvgl__lvgl/src/core'
changes={
    'lv_refr.c':[
        ('        if(layer_type == LV_LAYER_TYPE_SIMPLE) {\n            int32_t w = lv_area_get_width(&layer_area_full);',
         '        // TAB5_ORTHOGONAL_LAYER_TILES: right-angle objects need no full-frame layer.\n'
         '        int32_t tab5_rotation = lv_obj_get_style_transform_rotation(obj, 0);\n'
         '        bool tab5_orthogonal = layer_type == LV_LAYER_TYPE_TRANSFORM &&\n'
         '            lv_area_get_width(&layer_area_full) <= 2048 &&\n'
         '            (tab5_rotation == 900 || tab5_rotation == 1800 || tab5_rotation == 2700) &&\n'
         '            lv_obj_get_style_transform_scale_x(obj, 0) == LV_SCALE_NONE &&\n'
         '            lv_obj_get_style_transform_scale_y(obj, 0) == LV_SCALE_NONE &&\n'
         '            lv_obj_get_style_transform_skew_x(obj, 0) == 0 &&\n'
         '            lv_obj_get_style_transform_skew_y(obj, 0) == 0 &&\n'
         '            lv_obj_get_style_bitmap_mask_src(obj, 0) == NULL;\n'
         '        if(layer_type == LV_LAYER_TYPE_SIMPLE || tab5_orthogonal) {\n'
         '            int32_t w = lv_area_get_width(&layer_area_full);'),
        ('            bool area_need_alpha = alpha_test_area_on_obj(obj, &layer_area_act);',
         '            bool area_need_alpha = alpha_test_area_on_obj(obj, tab5_orthogonal ? &layer_area_full : &layer_area_act);'),
        ('            lv_layer_t * new_layer = lv_draw_layer_create(layer,\n'
         '                                                          area_need_alpha ? LV_COLOR_FORMAT_ARGB8888 : LV_COLOR_FORMAT_NATIVE, &layer_area_act);',
         '            // TAB5_ORTHOGONAL_HALO: preserve neighbour samples at tile edges.\n'
         '            lv_area_t tab5_tile_area = layer_area_act;\n'
         '            if(tab5_orthogonal) {\n'
         '                tab5_tile_area.y1 = LV_MAX(layer_area_full.y1, layer_area_act.y1 - 2);\n'
         '                tab5_tile_area.y2 = LV_MIN(layer_area_full.y2, layer_area_act.y2 + 2);\n'
         '            }\n'
         '            lv_layer_t * new_layer = lv_draw_layer_create(layer,\n'
         '                                                          area_need_alpha ? LV_COLOR_FORMAT_ARGB8888 : LV_COLOR_FORMAT_NATIVE, &tab5_tile_area);'),
        ('            lv_draw_layer(layer, &layer_draw_dsc, &layer_area_act);',
         '            lv_area_t tab5_saved_clip = layer->_clip_area;\n'
         '            if(tab5_orthogonal) {\n'
         '                lv_point_t edge[2] = {{obj->coords.x1, layer_area_act.y1}, {obj->coords.x1, layer_area_act.y2}};\n'
         '                lv_obj_transform_point_array(obj, edge, 2, LV_OBJ_POINT_TRANSFORM_FLAG_NONE);\n'
         '                // Only internal edges are clipped: retain original outer-edge antialiasing.\n'
         '                if(tab5_rotation == 900) {\n'
         '                    if(layer_area_act.y1 != layer_area_full.y1) layer->_clip_area.x2 = LV_MIN(layer->_clip_area.x2, edge[0].x);\n'
         '                    if(layer_area_act.y2 != layer_area_full.y2) layer->_clip_area.x1 = LV_MAX(layer->_clip_area.x1, edge[1].x);\n'
         '                } else if(tab5_rotation == 2700) {\n'
         '                    if(layer_area_act.y1 != layer_area_full.y1) layer->_clip_area.x1 = LV_MAX(layer->_clip_area.x1, edge[0].x);\n'
         '                    if(layer_area_act.y2 != layer_area_full.y2) layer->_clip_area.x2 = LV_MIN(layer->_clip_area.x2, edge[1].x);\n'
         '                } else {\n'
         '                    if(layer_area_act.y1 != layer_area_full.y1) layer->_clip_area.y2 = LV_MIN(layer->_clip_area.y2, edge[0].y);\n'
         '                    if(layer_area_act.y2 != layer_area_full.y2) layer->_clip_area.y1 = LV_MAX(layer->_clip_area.y1, edge[1].y);\n'
         '                }\n'
         '            }\n'
         '            lv_draw_layer(layer, &layer_draw_dsc, &tab5_tile_area);\n'
         '            layer->_clip_area = tab5_saved_clip;'),
        ('static void refr_area_part(lv_layer_t * layer)\n{',
         'bool (*tab5_lvgl_native_render)(lv_layer_t *) = NULL;\n'
         'static void refr_area_part(lv_layer_t * layer)\n{'),
        ('    /*If the screen is transparent initialize it when the flushing is ready*/',
         '    // TAB5_NATIVE_CACHE: keep the normal draw-buffer and DMA ownership path.\n'
         '    if(!disp_refr->prev_scr && tab5_lvgl_native_render && tab5_lvgl_native_render(layer)) {\n'
         '        draw_buf_flush(disp_refr);\n'
         '        LV_PROFILER_END;\n'
         '        return;\n'
         '    }\n'
         '    /*If the screen is transparent initialize it when the flushing is ready*/'),
        ('void lv_obj_redraw(lv_layer_t * layer, lv_obj_t * obj)\n{',
         '// TAB5_RENDER_CACHE_HOOKS\n'
         'bool (*tab5_lvgl_cache_ready)(const lv_obj_t *) = NULL;\n'
         'bool (*tab5_lvgl_cache_draw)(lv_layer_t *, lv_obj_t *) = NULL;\n'
         'void (*tab5_lvgl_cache_dirty)(const lv_obj_t *, const lv_area_t *) = NULL;\n\n'
         'void lv_obj_redraw(lv_layer_t * layer, lv_obj_t * obj)\n{\n'
         '    if(tab5_lvgl_cache_draw && tab5_lvgl_cache_draw(layer, obj)) return;'),
        ('    if(info.res == LV_COVER_RES_MASKED) return NULL;\n',
         '    if(info.res == LV_COVER_RES_MASKED) return NULL;\n'
         '    if(info.res == LV_COVER_RES_COVER && tab5_lvgl_cache_ready && tab5_lvgl_cache_ready(obj)) return obj;\n'),
    ],
    'lv_obj_pos.c':[
        ('void lv_obj_get_transformed_area(const lv_obj_t * obj, lv_area_t * area, lv_obj_point_transform_flag_t flags)\n{',
         'void lv_obj_get_transformed_area(const lv_obj_t * obj, lv_area_t * area, lv_obj_point_transform_flag_t flags)\n{\n'
         '    // TAB5_IDENTITY_DAMAGE: an identity transform must preserve inclusive bounds.\n'
         '    // The generic corner path conservatively adds one pixel on the right/bottom;\n'
         '    // that single pixel prevents opaque children from covering any scroll strip.\n'
         '#if !LV_DRAW_TRANSFORM_USE_MATRIX\n'
         '    const lv_obj_t * ancestor = obj;\n'
         '    while(ancestor) {\n'
         '        if(lv_obj_get_layer_type(ancestor) == LV_LAYER_TYPE_TRANSFORM) break;\n'
         '        if(!(flags & LV_OBJ_POINT_TRANSFORM_FLAG_RECURSIVE)) {ancestor = NULL; break;}\n'
         '        ancestor = lv_obj_get_parent(ancestor);\n'
         '    }\n'
         '    if(!ancestor) return;\n'
         '#endif'),
        ('void lv_obj_invalidate_area(const lv_obj_t * obj, const lv_area_t * area)\n{',
         'extern void (*tab5_lvgl_cache_dirty)(const lv_obj_t *, const lv_area_t *);\n'
         'void lv_obj_invalidate_area(const lv_obj_t * obj, const lv_area_t * area)\n{\n'
         '    if(tab5_lvgl_cache_dirty) tab5_lvgl_cache_dirty(obj, area);'),
    ],
}
for name,replacements in changes.items():
    path=root/name;text=path.read_text(encoding='utf-8');original=text
    # Upgrade the exact previous local hook; unrelated source layouts still fail.
    text=text.replace('(*tab5_lvgl_cache_dirty)(const lv_obj_t *)',
                      '(*tab5_lvgl_cache_dirty)(const lv_obj_t *, const lv_area_t *)')
    text=text.replace('tab5_lvgl_cache_dirty(obj);','tab5_lvgl_cache_dirty(obj, area);')
    text=text.replace('bool tab5_orthogonal = layer_type == LV_LAYER_TYPE_TRANSFORM &&\n'
                      '            (tab5_rotation',
                      'bool tab5_orthogonal = layer_type == LV_LAYER_TYPE_TRANSFORM &&\n'
                      '            lv_area_get_width(&layer_area_full) <= 2048 &&\n'
                      '            (tab5_rotation')
    for old,new in replacements:
        if new in text:continue
        if text.count(old)!=1:raise SystemExit(f'Unsupported cache hook source: {name}')
        text=text.replace(old,new,1)
    if text!=original:path.write_text(text,encoding='utf-8',newline='\n')
print('LVGL_RENDER_CACHE_HOOKS_OK')
