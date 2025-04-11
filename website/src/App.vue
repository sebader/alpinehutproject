<template>
  <div>
    <div class="header">
      <h4>{{ $t("message.siteTitle") }}</h4>
      <router-link :to="{ name: 'mapPage' }">{{ $t("message.map") }}</router-link> |
      <router-link :to="{ name: 'hutListPage' }">{{ $t("message.list") }}</router-link> |
      <router-link :to="{ name: 'infoPage' }">{{ $t("message.info") }}</router-link> |
      <router-link :to="{ name: 'adminPage' }">{{ $t("message.admin") }}</router-link>

      <span style="float:right;">
        <span v-for="locale in $i18n.availableLocales" :key="`locale-${locale}`">
          <router-link
            :to="{ name: $route.name, params: $route.params, query: $route.query, hash: $route.hash, replace: true }"
            :class="{ 'disabled-link': $i18n.locale === locale }" v-if="$i18n.locale !== locale"
            @click.native="$i18n.locale = locale">{{ $t("message.locale" + locale) }}</router-link>
          <span v-else>{{ $t("message.locale" + locale) }}</span>
          <span v-if="locale !== $i18n.availableLocales[$i18n.availableLocales.length - 1]"> | </span>
        </span>
      </span>
      <hr />

      <SystemMessage />
    </div>
    <router-view :key="$route.fullPath"></router-view>

    <hr />

    <footer>{{ $t("message.footerText") }}. Commit version: {{ versionLabel }}
    </footer>
  </div>
</template>

<script>
import SystemMessage from "./components/SystemMessage";

export default {
  data: function () {
    return {
      versionLabel: window.VERSION_LABEL
    }
  },
  created() { },
  methods: {},
  computed: {},
  components: {
    SystemMessage,
  },
};
</script>

<style>
footer {
  font-style: italic;
  font-size: 0.8em;
}</style>
