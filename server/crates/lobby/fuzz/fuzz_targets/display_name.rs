#![no_main]

use blastlands_lobby::names::DisplayName;
use libfuzzer_sys::fuzz_target;

fuzz_target!(|text: &str| {
    if let Ok(name) = DisplayName::new(text) {
        let shown = name.as_str();
        assert_eq!(shown, shown.trim());
        assert!((2..=16).contains(&shown.chars().count()));
        assert!(shown
            .chars()
            .all(|c| c.is_alphanumeric() || matches!(c, ' ' | '-' | '_')));

        assert_eq!(DisplayName::new(shown).ok().as_ref(), Some(&name));

        let json = serde_json::to_string(&name).expect("a name serializes");
        let back: DisplayName = serde_json::from_str(&json).expect("a serialized name parses");
        assert_eq!(back, name);
    }

    if let Ok(name) = serde_json::from_str::<DisplayName>(text) {
        assert!((2..=16).contains(&name.as_str().chars().count()));
    }
});
