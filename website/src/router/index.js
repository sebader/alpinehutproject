import { createRouter, createWebHistory } from "vue-router";

// Route components are lazy-loaded so each page ships in its own chunk,
// keeping the initial bundle small.
export default createRouter({
   history: createWebHistory(),
   routes: [
      {
         path: "/",
         redirect: "/map",
      },
      {
         path: "/map",
         name: "mapPage",
         component: () => import("../components/MapPage.vue"),
      },
      {
         path: "/hut",
         name: "hutListPage",
         component: () => import("../components/HutListPage.vue"),
      },
      {
         path: "/hut/:hutId",
         name: "hutDetailsPage",
         component: () => import("../components/HutDetailPage.vue"),
      },
      {
         path: "/info",
         name: "infoPage",
         component: () => import("../components/InfoPage.vue"),
      },
      {
         // Any unknown path falls back to the root (map) instead of a blank page.
         path: "/:pathMatch(.*)*",
         redirect: "/",
      },
   ],
});
