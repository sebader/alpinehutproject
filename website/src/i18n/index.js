import { createI18n } from "vue-i18n";

import en from "./en";
import de from "./de";

export default createI18n({
   locale: "de", // set locale
   fallbackLocale: "en", // set fallback locale
   messages: { en, de },
});
