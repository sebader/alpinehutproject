<template>
   <div :class="{ 'map-layout': isMapPage }">
      <div class="header" :class="{ 'map-header': isMapPage }" ref="appHeader">
         <div class="header-top">
            <h4 class="site-title">
               <img class="site-logo" src="/favicon.svg" alt="" aria-hidden="true" /> {{ $t("message.siteTitle") }}
            </h4>

            <div class="header-actions">
               <button
                  class="theme-toggle"
                  type="button"
                  @click="cycleTheme"
                  :title="themeLabel"
                  :aria-label="themeLabel"
               >
                  <svg
                     v-if="theme === 'light'"
                     viewBox="0 0 24 24"
                     fill="none"
                     stroke="currentColor"
                     stroke-width="2"
                     stroke-linecap="round"
                     stroke-linejoin="round"
                     aria-hidden="true"
                  >
                     <circle cx="12" cy="12" r="4" />
                     <path
                        d="M12 2v2M12 20v2M4.93 4.93l1.41 1.41M17.66 17.66l1.41 1.41M2 12h2M20 12h2M6.34 17.66l-1.41 1.41M19.07 4.93l-1.41 1.41"
                     />
                  </svg>
                  <svg
                     v-else-if="theme === 'dark'"
                     viewBox="0 0 24 24"
                     fill="none"
                     stroke="currentColor"
                     stroke-width="2"
                     stroke-linecap="round"
                     stroke-linejoin="round"
                     aria-hidden="true"
                  >
                     <path d="M21 12.79A9 9 0 1 1 11.21 3 7 7 0 0 0 21 12.79z" />
                  </svg>
                  <svg
                     v-else
                     viewBox="0 0 24 24"
                     fill="none"
                     stroke="currentColor"
                     stroke-width="2"
                     stroke-linecap="round"
                     stroke-linejoin="round"
                     aria-hidden="true"
                  >
                     <rect x="2" y="3" width="20" height="14" rx="2" />
                     <path d="M8 21h8M12 17v4" />
                  </svg>
               </button>

               <span class="lang-switcher">
                  <span v-for="locale in $i18n.availableLocales" :key="`locale-${locale}`">
                     <router-link
                        :to="{
                           name: $route.name,
                           params: $route.params,
                           query: $route.query,
                           hash: $route.hash,
                           replace: true,
                        }"
                        :class="{ 'disabled-link': $i18n.locale === locale }"
                        v-if="$i18n.locale !== locale"
                        @click="$i18n.locale = locale"
                        >{{ locale === "de" ? "🇩🇪" : "🇬🇧" }}</router-link
                     >
                     <span v-else class="active-locale">{{ locale === "de" ? "🇩🇪" : "🇬🇧" }}</span>
                  </span>
               </span>
            </div>
         </div>

         <nav class="main-nav">
            <router-link class="nav-tab" :to="{ name: 'mapPage' }">{{ $t("message.map") }}</router-link>
            <router-link class="nav-tab" :to="{ name: 'hutListPage' }">{{ $t("message.list") }}</router-link>
            <router-link class="nav-tab" :to="{ name: 'infoPage' }">{{ $t("message.info") }}</router-link>
         </nav>

         <SystemMessage v-if="!isMapPage" />
      </div>
      <router-view :key="$route.fullPath" :isAuthenticated="isAdmin"></router-view>

      <hr v-if="!isMapPage" />

      <footer v-if="!isMapPage">{{ $t("message.footerText") }}. Commit version: {{ versionLabel }}</footer>
   </div>
</template>

<script>
import SystemMessage from "./components/SystemMessage.vue";

export default {
   data: function () {
      let stored;
      try {
         stored = localStorage.getItem("theme");
      } catch {
         stored = null;
      }
      const mql = window.matchMedia("(prefers-color-scheme: dark)");
      return {
         versionLabel: window.VERSION_LABEL,
         isAdmin: false,
         theme: stored === "light" || stored === "dark" ? stored : "auto",
         systemDark: mql.matches,
      };
   },
   created() {
      // Check if user has admin role using SWA client principal
      fetch("/.auth/me")
         .then((response) => response.json())
         .then((authData) => {
            if (authData.clientPrincipal?.userRoles?.includes("admin")) {
               this.isAdmin = true;
            }
         })
         .catch(() => {
            this.isAdmin = false;
         });
   },
   mounted() {
      // Keep Bootstrap's data-bs-theme in sync with the OS preference while in "auto".
      this._mql = window.matchMedia("(prefers-color-scheme: dark)");
      this._mqlHandler = (e) => {
         this.systemDark = e.matches;
         if (this.theme === "auto") {
            document.documentElement.setAttribute("data-bs-theme", e.matches ? "dark" : "light");
         }
      };
      this._mql.addEventListener("change", this._mqlHandler);
      this.applyTheme();

      // Expose the header's bottom edge as a CSS variable so the fixed map and
      // its controls can sit just below the header regardless of its height
      // (which varies with the nav wrapping, locale, or viewport width).
      this.measureHeader();
      this._headerObserver = new ResizeObserver(() => this.measureHeader());
      if (this.$refs.appHeader) this._headerObserver.observe(this.$refs.appHeader);
      window.addEventListener("resize", (this._onResize = () => this.measureHeader()));
   },
   beforeUnmount() {
      if (this._mql) this._mql.removeEventListener("change", this._mqlHandler);
      if (this._headerObserver) this._headerObserver.disconnect();
      if (this._onResize) window.removeEventListener("resize", this._onResize);
   },
   methods: {
      measureHeader() {
         const el = this.$refs.appHeader;
         if (!el) return;
         const bottom = Math.round(el.getBoundingClientRect().bottom);
         document.documentElement.style.setProperty("--map-header-bottom", `${bottom}px`);
      },
      applyTheme() {
         const root = document.documentElement;
         if (this.theme === "auto") {
            root.removeAttribute("data-theme");
            root.setAttribute("data-bs-theme", this.systemDark ? "dark" : "light");
            try {
               localStorage.removeItem("theme");
            } catch {
               /* ignore */
            }
         } else {
            root.setAttribute("data-theme", this.theme);
            root.setAttribute("data-bs-theme", this.theme);
            try {
               localStorage.setItem("theme", this.theme);
            } catch {
               /* ignore */
            }
         }
      },
      cycleTheme() {
         const order = ["auto", "light", "dark"];
         this.theme = order[(order.indexOf(this.theme) + 1) % order.length];
         this.applyTheme();
      },
   },
   watch: {
      // Keep the browser tab title in sync with the current page and locale.
      pageTitle: {
         immediate: true,
         handler(title) {
            document.title = title;
         },
      },
      // The header height differs between the map page and the others, so
      // recompute the header-bottom variable after each navigation.
      $route() {
         this.$nextTick(() => this.measureHeader());
      },
   },
   computed: {
      isMapPage() {
         return this.$route.name === "mapPage";
      },
      themeLabel() {
         return `${this.$t("message.theme")}: ${this.$t("message.theme_" + this.theme)}`;
      },
      pageTitle() {
         const baseTitle = this.$t("message.siteTitle");
         if (this.$route.name === "infoPage") return `${baseTitle} - ${this.$t("message.info")}`;
         if (this.$route.name === "hutListPage") return `${baseTitle} - ${this.$t("message.list")}`;
         if (this.$route.name === "mapPage") return `${baseTitle} - ${this.$t("message.map")}`;
         return baseTitle;
      },
   },
   components: {
      SystemMessage,
   },
};
</script>

<style>
.map-layout {
   height: 100vh;
   overflow: hidden;
   position: relative;
}

.map-layout .header {
   position: relative;
   z-index: 2500;
   background: var(--surface);
   padding: 6px 15px;
   margin: 0;
   border-bottom: 3px solid var(--accent);
   box-shadow: 0 1px 4px var(--shadow);
}

.header {
   background: var(--surface);
   padding: 10px;
   margin-bottom: 20px;
   border-bottom: 3px solid var(--accent);
}

/* Header top row: title + actions (theme toggle + language switcher) */
.header-top {
   display: flex;
   align-items: center;
   justify-content: space-between;
   gap: 12px;
}

.site-title {
   min-width: 0;
   margin: 0;
   color: var(--text);
}

.site-logo {
   height: 1.4em;
   width: 1.4em;
   vertical-align: -0.3em;
   margin-right: 6px;
}

.header-actions {
   display: flex;
   align-items: center;
   gap: 12px;
   flex-shrink: 0;
}

.theme-toggle {
   display: inline-flex;
   align-items: center;
   justify-content: center;
   width: 32px;
   height: 32px;
   padding: 0;
   border: 1px solid var(--border);
   border-radius: 8px;
   background: var(--surface);
   color: var(--text);
   cursor: pointer;
   transition:
      background-color 0.2s ease,
      color 0.2s ease,
      border-color 0.2s ease;
}

.theme-toggle:hover {
   border-color: var(--accent);
   color: var(--accent);
}

.theme-toggle svg {
   width: 17px;
   height: 17px;
}

.lang-switcher {
   display: flex;
   align-items: center;
   gap: 8px;
   font-size: 1.15em;
   white-space: nowrap;
   flex-shrink: 0;
}

.lang-switcher a {
   text-decoration: none;
   opacity: 0.55;
   transition:
      opacity 0.2s ease,
      transform 0.2s ease;
}

.lang-switcher a:hover {
   opacity: 1;
   transform: scale(1.2);
}

.active-locale {
   filter: none;
}

/* Navigation tabs */
.main-nav {
   display: flex;
   flex-wrap: wrap;
   gap: 8px;
   margin-top: 8px;
}

.nav-tab {
   display: inline-block;
   padding: 5px 14px;
   border-radius: 20px;
   background: var(--surface-tint);
   color: var(--text);
   font-weight: 600;
   font-size: 0.95em;
   text-decoration: none;
   transition:
      background-color 0.2s ease,
      color 0.2s ease;
}

.nav-tab:hover {
   background: var(--surface-tint-hover);
}

.nav-tab.router-link-active {
   background: var(--accent);
   color: #fff;
}

/* Keep the map-page header compact so it doesn't overlap the map controls */
.map-header .main-nav {
   margin-top: 6px;
}

.map-header .nav-tab {
   padding: 4px 12px;
}

footer {
   font-style: italic;
   font-size: 0.8em;
}

.disabled-link {
   opacity: 0.5;
   cursor: default;
   text-decoration: none;
   font-size: 1.2em;
}

.header h4 {
   white-space: nowrap;
   overflow: hidden;
   text-overflow: ellipsis;
   margin-right: 10px;
}
</style>
