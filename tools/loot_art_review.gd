extends SceneTree
const IDS = ["jerry_can","motor_oil","water","bread","medkit","duct_tape","wrench",
    "spark_plug","cassette","cigarettes","spare_tire","map","radio_part","scrap_metal","bottle_cap"]
const NAMES = ["Канистра","Моторное масло","Вода","Хлеб","Аптечка","Изолента","Ключ",
    "Свеча","Кассета","Сигареты","Запаска","Карта","Радиодеталь","Металлолом","Пробка"]
var frames := 0
var output := "user://loot-art.png"
var view := "icons"
func _initialize():
    for arg in OS.get_cmdline_user_args():
        if arg.begins_with("--art-output="): output=arg.trim_prefix("--art-output=")
        if arg.begins_with("--art-view="): view=arg.trim_prefix("--art-view=")
    call_deferred("setup")
func setup():
    if view == "backpack":
        var world=load("res://scenes/Main.tscn").instantiate()
        root.add_child(world); world.set_physics_process(false); paused=false
        return
    var layer=CanvasLayer.new();root.add_child(layer)
    var background=ColorRect.new();background.color=Color(.065,.075,.08)
    background.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT);layer.add_child(background)
    var heading=Label.new();heading.text="ПРЕДМЕТЫ • МОДЕЛИ И ИКОНКИ";heading.position=Vector2(38,20)
    heading.add_theme_font_size_override("font_size",26);layer.add_child(heading)
    for i in range(IDS.size()):
        var x=36+(i%5)*225;var y=70+(i/5)*210
        var tile=ColorRect.new();tile.color=Color(.105,.12,.13);tile.position=Vector2(x,y);tile.size=Vector2(212,196);layer.add_child(tile)
        var picture=TextureRect.new();picture.texture=load("res://assets/models/props/icons/"+IDS[i]+".png")
        picture.expand_mode=TextureRect.EXPAND_IGNORE_SIZE;picture.stretch_mode=TextureRect.STRETCH_KEEP_ASPECT_CENTERED
        picture.position=Vector2(x+26,y+4);picture.size=Vector2(160,160)
        layer.add_child(picture)
        var label=Label.new();label.text=NAMES[i];label.position=Vector2(x,y+166);label.size=Vector2(212,26)
        label.horizontal_alignment=HORIZONTAL_ALIGNMENT_CENTER;layer.add_child(label)
func _process(_delta):
    frames+=1
    if frames==90:
        await RenderingServer.frame_post_draw
        var result=root.get_texture().get_image().save_png(output)
        print("[loot-shot] ",output," result=",result)
        quit(0 if result==OK else 1)
    return false
