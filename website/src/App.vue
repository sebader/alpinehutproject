<template>
  <div :class="{ 'map-layout': $route.name === 'mapPage' }">
    <div class="header" :class="{ 'map-header': $route.name === 'mapPage' }">
      <h4>{{ pageTitle }}</h4>
      <router-link :to="{ name: 'mapPage' }">{{ $t("message.map") }}</router-link> |
      <router-link :to="{ name: 'hutListPage' }">{{ $t("message.list") }}</router-link> |
      <router-link :to="{ name: 'infoPage' }">{{ $t("message.info") }}</router-link>

      <span style="float:right;">
        <span v-for="locale in $i18n.availableLocales" :key="`locale-${locale}`">
          <router-link
            :to="{ name: $route.name, params: $route.params, query: $route.query, hash: $route.hash, replace: true }"
            :class="{ 'disabled-link': $i18n.locale === locale }" v-if="$i18n.locale !== locale"
            @click.native="$i18n.locale = locale">{{ locale === 'de' ? '🇩🇪' : '🇬🇧' }}</router-link>
          <span v-else>{{ locale === 'de' ? '🇩🇪' : '🇬🇧' }}</span>
          <span v-if="locale !== $i18n.availableLocales[$i18n.availableLocales.length - 1]"> | </span>
        </span>
      </span>
      <hr v-if="$route.name !== 'mapPage'" />

      <SystemMessage v-if="$route.name !== 'mapPage'" />
    </div>
    <router-view :key="$route.fullPath" :isAuthenticated="isAdmin"></router-view>

    <hr v-if="$route.name !== 'mapPage'" />

    <footer v-if="$route.name !== 'mapPage'">{{ $t("message.footerText") }}. Commit version: {{ versionLabel }}
    </footer>
  </div>
</template>

<script>
import SystemMessage from "./components/SystemMessage";

export default {
  data: function () {
    return {
      versionLabel: window.VERSION_LABEL,
      isAdmin: false
    }
  },
  created() {
    // Check if user has admin role using SWA client principal
    fetch('/.auth/me')
      .then(response => response.json())
      .then(authData => {
        if (authData.clientPrincipal?.userRoles?.includes('admin')) {
          this.isAdmin = true;
        }
      })
      .catch(() => {
        this.isAdmin = false;
      });
  },
  methods: {},
  computed: {
    pageTitle() {
      const baseTitle = this.$t("message.siteTitle");
      if (this.$route.name === 'infoPage') return `${baseTitle} - ${this.$t('message.info')}`;
      if (this.$route.name === 'hutListPage') return `${baseTitle} - ${this.$t('message.list')}`;
      if (this.$route.name === 'mapPage') return `${baseTitle} - ${this.$t('message.map')}`;
      return baseTitle;
    }
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
  padding: 5px 15px;
  margin: 0;
  border-bottom: 1px solid #eee;
  box-shadow: 0 1px 4px rgba(0,0,0,0.1);
}

.header {
  background: #fff;
  padding: 10px;
  margin-bottom: 20px;
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
}</style>
