import { createApp } from 'vue'
import { createRouter, createWebHistory } from 'vue-router'
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
         siteTitle: 'Alpine Huts Booking',
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
         hutWebsite: 'Hut Website',
         submit: 'Submit',
         formSuccessfullySubmitted: 'Successfully submitted',
         admin: 'Admin',
         editHut: 'Edit Hut',
         delete: 'Delete',
         edit: 'Edit',
         save: 'Save',
         cancel: 'Cancel',
         confirmDelete: 'Confirm Delete',
         confirmDeleteText: 'Are you sure you want to delete the hut',
         yes: 'Yes',
         no: 'No',
         enabled: 'Enabled',
         isDeleted: 'Deleted',
         website: 'Website',
         onlineBookingLink: 'Online Booking Link',
         latitude: 'Latitude',
         longitude: 'Longitude',
         altitude: 'Altitude',
         actions: 'Actions',
         name: 'Name',
         login: 'Login (admin)',
         logout: 'Logout',
         manuallyEdited: 'Manually Edited',
         addLocation: 'Add Location',
         filterByWeekdays: 'Filter by Weekdays',
         searchHuts: 'Search huts...',
         noResultsFound: 'No availability found for selected weekday(s)',
         clearFilter: 'Clear Filter',
         noAvailabilityInfo: 'No availability information available. Please check the booking page directly.',
         allDatesClosedTitle: 'All dates are currently closed',
         sourceAlpenvereine: 'Alpenvereine',
         sourceHuettenHoliday: 'Huetten-Holiday',
         source: 'Source',
         sunday: 'Sunday',
         monday: 'Monday',
         tuesday: 'Tuesday',
         wednesday: 'Wednesday',
         thursday: 'Thursday',
         friday: 'Friday',
         saturday: 'Saturday',
         january: 'January',
         february: 'February',
         march: 'March',
         april: 'April',
         may: 'May',
         june: 'June',
         july: 'July',
         august: 'August',
         september: 'September',
         october: 'October',
         november: 'November',
         december: 'December',
         mon: 'Mon',
         tue: 'Tue',
         wed: 'Wed',
         thu: 'Thu',
         fri: 'Fri',
         sat: 'Sat',
         sun: 'Sun',
      },
      mapPage: {
         availabilityAtDate: 'Availability on',
         numberOfBeds: 'Number of beds',
         bedCategory: 'Bed category',
         freeBeds: 'Free beds',
         noAvailabilityInfo: 'No availability information available. Please check the booking page directly.',
         anyBeds: 'any',
         zoomIn: 'Zoom',
      },
      hutListPage: {
         allHuts: 'All huts',
         hutCount: '{count} Huts',
      },
      infoPage: {
         title: 'Alpine Huts Project',
         infoHeader: 'Welcome',
         aboutTitle: 'About This Project',
         howItWorksTitle: 'How It Works',
         openSourceTitle: 'Open Source',
         viewOnGithub: 'View on GitHub',
         firstParagraph:'This comprehensive platform aggregates alpine hut availability from two primary sources: huts connected to the central booking system of the alpine clubs of Switzerland, Austria, Germany and Southern-Tyrol (Italy), as well as accommodations from huetten-holiday.com.',
         secondParagraph: 'Our system monitors and updates availability information to provide booking insights for mountain enthusiasts. Users can search, filter, and track availability across hundreds of alpine huts throughout the Alpine region.',
         thirdParagraph: 'This project is developed as open-source. The complete source code is available on GitHub.',
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
         siteTitle: 'AV-Hüttenbuchung',
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
         hutWebsite: 'Hütten Webseite',
         submit: 'Absenden',
         formSuccessfullySubmitted: 'Erfolgreich abgesendet',
         admin: 'Admin',
         editHut: 'Hütte bearbeiten',
         delete: 'Löschen',
         edit: 'Bearbeiten',
         save: 'Speichern',
         cancel: 'Abbrechen',
         confirmDelete: 'Löschen bestätigen',
         confirmDeleteText: 'Sind Sie sicher, dass Sie die Hütte löschen möchten',
         yes: 'Ja',
         no: 'Nein',
         enabled: 'Aktiviert',
         isDeleted: 'Gelöscht',
         website: 'Webseite',
         onlineBookingLink: 'Online-Buchungslink',
         latitude: 'Breitengrad',
         longitude: 'Längengrad',
         altitude: 'Höhe',
         actions: 'Aktionen',
         name: 'Name',
         login: 'Anmelden (admin)',
         logout: 'Abmelden',
         manuallyEdited: 'Manuell bearbeitet',
         addLocation: 'Standort hinzufügen',
         filterByWeekdays: 'Filtern nach Wochentagen',
         searchHuts: 'Hütten suchen...',
         noResultsFound: 'Keine Verfügbarkeit für ausgewählte Wochentage gefunden',
         clearFilter: 'Filter zurücksetzen',
         noAvailabilityInfo: 'Keine Belegungsinfo verfügbar. Bitte direkt auf der Buchungsseite nachsehen.',
         allDatesClosedTitle: 'Alle Termine sind derzeit geschlossen',
         sourceAlpenvereine: 'Alpenvereine',
         sourceHuettenHoliday: 'Hütten-Holiday',
         source: 'Quelle',
         sunday: 'Sonntag',
         monday: 'Montag',
         tuesday: 'Dienstag',
         wednesday: 'Mittwoch',
         thursday: 'Donnerstag',
         friday: 'Freitag',
         saturday: 'Samstag',
         january: 'Januar',
         february: 'Februar',
         march: 'März',
         april: 'April',
         may: 'Mai',
         june: 'Juni',
         july: 'Juli',
         august: 'August',
         september: 'September',
         october: 'Oktober',
         november: 'November',
         december: 'Dezember',
         mon: 'Mo',
         tue: 'Di',
         wed: 'Mi',
         thu: 'Do',
         fri: 'Fr',
         sat: 'Sa',
         sun: 'So',
      },
      mapPage: {
         availabilityAtDate: 'Verfügbarkeit am',
         numberOfBeds: 'Anzahl Betten',
         bedCategory: 'Zimmerkategorie',
         freeBeds: 'Freie Betten',
         noAvailabilityInfo: 'Keine Belegungsinfo verfügbar. Bitte direkt auf der Buchungsseite nachsehen.',
         anyBeds: 'egal',
         zoomIn: 'Zoom',
      },
      hutListPage: {
         allHuts: 'Alle Hütten',
         hutCount: '{count} Hütten',
      },
      infoPage: {
         title: 'Alpine Hütten Projekt',
         infoHeader: 'Willkommen',
         aboutTitle: 'Über dieses Projekt',
         howItWorksTitle: 'Funktionsweise',
         openSourceTitle: 'Open Source',
         viewOnGithub: 'Auf GitHub anzeigen',
         firstParagraph:'Diese umfassende Plattform aggregiert die Verfügbarkeit von Berghütten aus zwei Hauptquellen: Hütten, die mit dem zentralen Buchungssystem der Alpenvereine der Schweiz, Österreich, Deutschland und Südtirol (Italien) verbunden sind, sowie Unterkünfte von huetten-holiday.com.',
         secondParagraph: 'Unser System aktualisiert kontinuierlich die Verfügbarkeitsinformationen, um Bergsteigern aktuelle Belegungsinformationen zu bieten. Benutzer können die Verfügbarkeit von Hunderten von Berghütten im gesamten Alpenraum suchen, filtern und verfolgen.',
         thirdParagraph: 'Dieses Projekt wird als Open-Source entwickelt. Der vollständige Quellcode ist auf GitHub verfügbar.',
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
   history: createWebHistory(),
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
