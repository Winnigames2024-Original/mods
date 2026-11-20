package com.winnigames2024.geodash.setup;

/** Лицензионный ключ: тот же алфавит и та же контрольная сумма, что в игре. */
public class Keys {

    private static final String ALPHA = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

    public static String normalize(String s) {
        if (s == null) return "";
        StringBuilder sb = new StringBuilder(16);
        for (char ch : s.toUpperCase().toCharArray()) {
            char c = ch;
            if (c == 'O') c = '0';
            else if (c == 'I' || c == 'L') c = '1';
            else if (c == 'U') c = 'V';
            if (ALPHA.indexOf(c) >= 0) sb.append(c);
        }
        String t = sb.toString();
        if (t.startsWith("GD") && t.length() - 2 >= 16) t = t.substring(2);
        return t.length() > 16 ? t.substring(0, 16) : t;
    }

    private static char checkChar(String body) {
        int sum = 0;
        for (int i = 0; i < body.length(); i++) {
            int v = ALPHA.indexOf(body.charAt(i));
            sum += (v < 0 ? 0 : v) * (i + 2);
        }
        return ALPHA.charAt(sum % 32);
    }

    public static boolean wellFormed(String s) {
        String raw = normalize(s);
        return raw.length() == 16 && checkChar(raw.substring(0, 15)) == raw.charAt(15);
    }

    public static String pretty(String s) {
        String raw = normalize(s);
        if (raw.isEmpty()) return "";
        StringBuilder sb = new StringBuilder("GD");
        for (int i = 0; i < raw.length(); i++) {
            if (i % 4 == 0) sb.append('-');
            sb.append(raw.charAt(i));
        }
        return sb.toString();
    }
}
