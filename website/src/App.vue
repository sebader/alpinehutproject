<template>
   <div :class="{ 'map-layout': isMapPage }">
      <div class="header" :class="{ 'map-header': isMapPage }">
         <div class="header-top">
            <h4 class="site-title">
               <img class="site-logo" src="/favicon.svg" alt="" aria-hidden="true" /> {{ $t("message.siteTitle") }}
            </h4>

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
      return {
         versionLabel: window.VERSION_LABEL,
         isAdmin: false,
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
   methods: {},
   watch: {
      // Keep the browser tab title in sync with the current page and locale.
      pageTitle: {
         immediate: true,
         handler(title) {
            document.title = title;
         },
      },
   },
   computed: {
      isMapPage() {
         return this.$route.name === "mapPage";
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
   background: #fff;
   padding: 6px 15px;
   margin: 0;
   border-bottom: 3px solid #2ecc71;
   box-shadow: 0 1px 4px rgba(0, 0, 0, 0.1);
}

.header {
   background: #fff;
   padding: 10px;
   margin-bottom: 20px;
   border-bottom: 3px solid #2ecc71;
}

/* Header top row: title + language switcher */
.header-top {
   display: flex;
   align-items: center;
   justify-content: space-between;
   gap: 12px;
}

.site-title {
   min-width: 0;
   margin: 0;
   color: #2c3e50;
}

.site-logo {
   height: 1.4em;
   width: 1.4em;
   vertical-align: -0.3em;
   margin-right: 6px;
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
   background: #f0f4f8;
   color: #2c3e50;
   font-weight: 600;
   font-size: 0.95em;
   text-decoration: none;
   transition:
      background-color 0.2s ease,
      color 0.2s ease;
}

.nav-tab:hover {
   background: #e4ebf5;
}

.nav-tab.router-link-active {
   background: #2ecc71;
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
