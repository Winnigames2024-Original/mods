package com.winnigames2024.sulak;

import net.minecraftforge.common.ForgeConfigSpec;
import net.minecraftforge.eventbus.api.listener.SubscribeEvent;
import net.minecraftforge.fml.common.Mod;
import net.minecraftforge.fml.event.config.ModConfigEvent;

@Mod.EventBusSubscriber(modid = Sulak.MODID, bus = Mod.EventBusSubscriber.Bus.MOD)
public class Config {

    private static final ForgeConfigSpec.Builder B = new ForgeConfigSpec.Builder();

    private static final ForgeConfigSpec.ConfigValue<String> API_URL = B
            .comment("Endpoint of the Sulak AI gateway (api.php)")
            .define("apiUrl", "https://ai.winnigames2024.ru/api/api.php");

    private static final ForgeConfigSpec.ConfigValue<String> API_KEY = B
            .comment("Server-wide API key. Get a free one at https://ai.winnigames2024.ru/api/index.php",
                     "Players may override it with /sulak key <key>")
            .define("apiKey", "");

    private static final ForgeConfigSpec.IntValue REQUEST_LIMIT = B
            .comment("How many wishes a player may ask Sulak before the limit runs out")
            .defineInRange("requestLimit", 10, 1, 10000);

    private static final ForgeConfigSpec.IntValue TIMEOUT = B
            .comment("HTTP timeout in seconds")
            .defineInRange("timeoutSeconds", 25, 5, 120);

    private static final ForgeConfigSpec.BooleanValue SPAWN_ON_JOIN = B
            .comment("Spawn Sulak next to a random online player when somebody joins")
            .define("spawnOnJoin", true);

    private static final ForgeConfigSpec.DoubleValue SPAWN_CHANCE = B
            .comment("Chance (0..1) that a join triggers a re-roll of Sulak's owner")
            .defineInRange("spawnChance", 1.0D, 0.0D, 1.0D);

    private static final ForgeConfigSpec.BooleanValue ALLOW_KILL = B
            .comment("Allow Sulak to kill / damage players")
            .define("allowKill", true);

    private static final ForgeConfigSpec.BooleanValue ALLOW_RAW_COMMANDS = B
            .comment("Allow the AI to run arbitrary vanilla commands (powerful - off by default)")
            .define("allowRawCommands", false);

    private static final ForgeConfigSpec.BooleanValue CURSE_ENABLED = B
            .comment("When a player's limit runs out, strange things start happening to them")
            .define("curseEnabled", true);

    private static final ForgeConfigSpec.IntValue CURSE_INTERVAL = B
            .comment("Average ticks between curse events (20 ticks = 1 second)")
            .defineInRange("curseIntervalTicks", 200, 20, 20 * 600);

    private static final ForgeConfigSpec.BooleanValue CURSE_SKIN = B
            .comment("Replace the skin of a cursed player (needs the mod on the client)")
            .define("curseChangesSkin", true);

    public static final ForgeConfigSpec SPEC = B.build();

    public static String apiUrl = "https://ai.winnigames2024.ru/api/api.php";
    public static String apiKey = "";
    public static int requestLimit = 10;
    public static int timeoutSeconds = 25;
    public static boolean spawnOnJoin = true;
    public static double spawnChance = 1.0D;
    public static boolean allowKill = true;
    public static boolean allowRawCommands = false;
    public static boolean curseEnabled = true;
    public static int curseIntervalTicks = 200;
    public static boolean curseChangesSkin = true;

    @SubscribeEvent
    static void onLoad(final ModConfigEvent.Loading event) {
        apply();
    }

    @SubscribeEvent
    static void onReload(final ModConfigEvent.Reloading event) {
        apply();
    }

    private static void apply() {
        apiUrl = API_URL.get();
        apiKey = API_KEY.get();
        requestLimit = REQUEST_LIMIT.get();
        timeoutSeconds = TIMEOUT.get();
        spawnOnJoin = SPAWN_ON_JOIN.get();
        spawnChance = SPAWN_CHANCE.get();
        allowKill = ALLOW_KILL.get();
        allowRawCommands = ALLOW_RAW_COMMANDS.get();
        curseEnabled = CURSE_ENABLED.get();
        curseIntervalTicks = CURSE_INTERVAL.get();
        curseChangesSkin = CURSE_SKIN.get();
    }
}
