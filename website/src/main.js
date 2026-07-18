import { createApp } from "vue";
import { ApplicationInsights } from "@microsoft/applicationinsights-web";

import "bootstrap/dist/css/bootstrap.css";

import SimpleTypeahead from "vue3-simple-typeahead";
import "vue3-simple-typeahead/dist/vue3-simple-typeahead.css"; //Optional default CSS

import Vue3EasyDataTable from "vue3-easy-data-table";
import "vue3-easy-data-table/dist/style.css";

import App from "./App.vue";
import i18n from "./i18n";
import router from "./router";

import HutService from "./services/hut-service";
import MapviewService from "./services/mapview-service";
import AvailabilityService from "./services/availability-service";
import BedCategoryService from "./services/bedcategory-service";
import NotificationService from "./services/notification-service";

const appInsights = new ApplicationInsights({
   config: {
      connectionString: window.APPLICATIONINSIGHTS_CONNECTION_STRING,
      enableCorsCorrelation: true,
      enableRequestHeaderTracking: true,
      enableResponseHeaderTracking: true,
      disableFetchTracking: false,
      enableAutoRouteTracking: true,
   },
});

appInsights.loadAppInsights();
appInsights.trackPageView();

const app = createApp(App);

app.use(SimpleTypeahead);
app.use(i18n);

app.component("EasyDataTable", Vue3EasyDataTable);

app.config.globalProperties.$HutService = new HutService();
app.config.globalProperties.$AvailabilityService = new AvailabilityService();
app.config.globalProperties.$MapviewService = new MapviewService();
app.config.globalProperties.$BedCategoryService = new BedCategoryService();
app.config.globalProperties.$NotificationService = new NotificationService();

app.use(router).mount("#app");
