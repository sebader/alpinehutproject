<template>
  <div>
    <h4>{{ $t("message.siteTitle") }}</h4>
    <router-link :to="{ name: 'mapPage' }">{{ $t("message.map") }}</router-link> |
    <router-link :to="{ name: 'hutListPage' }">{{ $t("message.list") }}</router-link> |
    <router-link :to="{ name: 'infoPage' }">{{ $t("message.info") }}</router-link>

    <span style="float:right;">
      {{ $t("message.language") }}
      <select v-model="$i18n.locale">
        <option v-for="locale in $i18n.availableLocales" :key="`locale-${locale}`" :value="locale">{{ $t("message.locale" + locale) }}</option>
      </select>
    </span>
    <hr />

    <SystemMessage />

    <router-view :key="$route.fullPath"></router-view>

    <hr />

    <footer>{{ $t("message.footerText")}}. Commit version: {{ versionLabel }}
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
}
</style>
