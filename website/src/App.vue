<template>
  <div>
    <router-link :to="{ name: 'mapPage' }">Map</router-link> |
    <router-link :to="{ name: 'hutListPage' }">List</router-link>

    <hr />

    <SystemMessage />

    <router-view :key="$route.fullPath"></router-view>

    <hr/>

    <footer>This is a a private project with no association to the Alpine Clubs. Version: {{ versionLabel }}</footer>
  </div>
</template>

<script>
import SystemMessage from "./components/SystemMessage";

export default {
  data: function() {
     return {
        versionLabel: window.VERSION_LABEL
      }
  },
  created() { },
  methods: { },
  computed: { },
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
