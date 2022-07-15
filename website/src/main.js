import { createApp } from 'vue'
import Vue from 'vue'
import { createRouter, createWebHashHistory } from 'vue-router'
import { BootstrapVue } from 'bootstrap-vue'
import { ApplicationInsights } from '@microsoft/applicationinsights-web'
import emitter from 'tiny-emitter/instance'

import App from './App.vue'
import components from "@/components"

// Import Bootstrap and BootstrapVue CSS files (order is important)
import 'bootstrap/dist/css/bootstrap.css'
import 'bootstrap-vue/dist/bootstrap-vue.css'

import HutService from './services/hut-service'
import MapviewService from './services/mapview-service'
import AvailabilityService from './services/availability-service'
import BedCategoryService from './services/bedcategory-service'

const appInsights = new ApplicationInsights(
{
   config: {
      connectionString: window.APPLICATIONINSIGHTS_CONNECTION_STRING,
      enableCorsCorrelation: true,
      enableRequestHeaderTracking: true,
      enableResponseHeaderTracking: true,
      disableFetchTracking: false,
      enableAutoRouteTracking: true
   }
});

appInsights.loadAppInsights();
appInsights.trackPageView();

// Make BootstrapVue available throughout your project
//Vue.use(BootstrapVue)

const app = createApp(App);

app.config.globalProperties.$HutService = new HutService();
app.config.globalProperties.$AvailabilityService = new AvailabilityService();
app.config.globalProperties.$MapviewService = new MapviewService();
app.config.globalProperties.$BedCategoryService = new BedCategoryService();

// global event bus to send events across components
// migrated to emitter based on https://v3.vuejs.org/guide/migration/events-api.html#event-bus
export const EventBus = {
   $on: (...args) => emitter.on(...args),
   $once: (...args) => emitter.once(...args),
   $off: (...args) => emitter.off(...args),
   $emit: (...args) => emitter.emit(...args)
}

const router = createRouter({
   history: createWebHashHistory(),
   routes: [
   {
      path: "/",
      name: "mapPage",
      component: components.MapPage
   },
   {
      path: "/hut",
      name: "hutsPage",
      component: components.HutsPage
   },
   {
      path: "/hut/:hutId",
      name: "hutById",
      component: components.HutDetailPage
   }
  ]
});

app.use(router).mount("#app");