import { createApp } from 'vue'
import { createRouter, createWebHashHistory } from 'vue-router'
import { ApplicationInsights } from '@microsoft/applicationinsights-web'
import emitter from 'tiny-emitter/instance'

import { createI18n } from 'vue-i18n'

import App from './App.vue'
import components from "@/components"

// Import Bootstrap and BootstrapVue CSS files (order is important)
import 'bootstrap/dist/css/bootstrap.css'

import SimpleTypeahead from 'vue3-simple-typeahead';
import 'vue3-simple-typeahead/dist/vue3-simple-typeahead.css'; //Optional default CSS

import Vue3EasyDataTable from 'vue3-easy-data-table';
import 'vue3-easy-data-table/dist/style.css';

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

const messages = {
   en: {
      message: {
         siteTitle: 'Alpine Huts - Online Booking',
         map: 'Map',
         list: 'List',
         info: 'Info',
         search: 'Search',
         language: 'Language',
         loading: 'Loading...',
         footerText: 'This is a private project with no association to the Alpine Clubs',
         onlineBooking: 'Online Booking',
         onlineBookingInactive: 'Online Booking Inactive',
         lastUpdated: 'Last Updated',
         hutfullNotify: 'Notify me when a space opens up',
         showHutDetails: 'Show hut details',
         showOnMap: 'Show on map',
         hut: 'Hut',
         country: 'Country',
         region: 'Region',
         coordinates: 'Coordinates',
         hutAdded: 'Hut added',
         date: 'Date',
         beds: 'Beds',
         typeOfAccommodation: 'Type of accommodation',
         localeen: 'English',
         localede: 'Deutsch',
         hutClosed: 'Hut closed',
         hutNotYetActive: 'Hut not yet activated',
         submit: 'Submit',
         formSuccessfullySubmitted: 'Successfully submitted',
      },
      mapPage: {
         availabilityAtDate: 'Availability on',
         numberOfBeds: 'Number of beds',
         bedCategory: 'Bed category',
         freeBeds: 'Free beds',
         noAvailabilityInfo: 'No availability information available',
         anyBeds: 'any',
      },
      hutListPage: {
         allHuts: 'All huts',
      },
      infoPage: {
         infoHeader: 'Welcome',
         firstParagraph:'This is a list of all alpine huts which are connected to the central booking system of the alpine clubs of Switzerland, Austria, Germany, Slowenia and Southern-Tyrol (Italy).',
         secondParagraph: 'Important: This is a private project with no connection to the alpine clubs. There is no guarantee about the correctness of the data shown.',
         thirdParagraph: 'The source code of this project is available on GitHub!',
      },
      countries: {
         Schweiz: 'Switzerland',
         Österreich: 'Austria',
         Deutschland: 'Germany',
         Italien: 'Italy',
         Slowenien: 'Slovenia',
         Belgien: 'Belgium',
         Frankreich: 'France',
         unbekannt: 'unknown',
      }
   },
   de: {
      message: {
         siteTitle: 'AV-Hütten Onlinebuchung',
         map: 'Karte',
         list: 'Liste',
         info: 'Info',
         search: 'Suche',
         language: 'Sprache',
         loading: 'Lade...',
         footerText: 'Dies ist ein privates Projekt ohne Verbindung zu den Alpenvereinen',
         onlineBooking: 'Online Buchung',
         onlineBookingInactive: 'Online Buchung nicht aktiv',
         lastUpdated: 'Letztes Update',
         hutfullNotify: 'Benachrichtige mich, wenn ein Platz frei wird',
         showHutDetails: 'Zeige Hütten Details',
         showOnMap: 'Zeige auf Karte',
         hut: 'Hütte',
         country: 'Land',
         region: 'Region',
         coordinates: 'Koordinaten',
         hutAdded: 'Hütte hinzugefügt',
         date: 'Datum',
         beds: 'Betten',
         typeOfAccommodation: 'Zimmerkategorie',
         localeen: 'English',
         localede: 'Deutsch',
         hutClosed: 'Hütte geschlossen',
         hutNotYetActive: 'Hütte noch nicht aktiviert',
         submit: 'Absenden',
         formSuccessfullySubmitted: 'Erfolgreich abgesendet',
      },
      mapPage: {
         availabilityAtDate: 'Verfügbarkeit am',
         numberOfBeds: 'Anzahl Betten',
         bedCategory: 'Zimmerkategorie',
         freeBeds: 'Freie Betten',
         noAvailabilityInfo: 'Keine Information verfügbar',
         anyBeds: 'egal',
      },
      hutListPage: {
         allHuts: 'Alle Hütten',
      },
      infoPage: {
         infoHeader: 'Willkommen',
         firstParagraph:'Dies ist eine Liste aller Alpenvereinshütten, die mit dem zentralen Buchungssystem der Alpenvereine der Schweiz, Österreich, Deutschland, Slowenien und Südtirol (Italien) verbunden sind.',
         secondParagraph: 'Wichtig: Dies ist ein privates Projekt ohne Verbindung zu den Alpenvereinen. Alle Angaben sind ohne Gewähr.',
         thirdParagraph: 'Der Quellcode dieses Projekts ist auf GitHub verfügbar!',
      },
      countries: {
         Schweiz: 'Schweiz',
         Österreich: 'Österreich',
         Deutschland: 'Deutschland',
         Italien: 'Italien',
         Slowenien: 'Slowenien',
         Belgien: 'Belgien',
         Frankreich: 'Frankreich',
         unbekannt: 'unbekannt',
      }
   }
}

const i18n = createI18n({
   locale: 'de', // set locale
   fallbackLocale: 'en', // set fallback locale
   messages, // set locale messages
   // If you need to specify other options, you can set other options
   // ...
})

const app = createApp(App);

app.use(SimpleTypeahead);
app.use(i18n)

app.component('EasyDataTable', Vue3EasyDataTable);

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
         path: '/',
         redirect: '/map'
      },
      {
         path: "/map",
         name: "mapPage",
         component: components.MapPage
      },
      {
         path: "/hut",
         name: "hutListPage",
         component: components.HutListPage
      },
      {
         path: "/hut/:hutId",
         name: "hutDetailsPage",
         component: components.HutDetailPage
      },
      {
         path: "/info",
         name: "infoPage",
         component: components.InfoPage
      },
   ]
});

app.use(router).mount("#app");