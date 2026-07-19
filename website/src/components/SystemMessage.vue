<template>
   <div v-show="message !== ''">
      <p :class="type">{{ message }} <button @click="message = ''">❌ Dismiss</button></p>
   </div>
</template>

<script>
import { Constants } from "../utils";
import { EventBus } from "../event-bus";

export default {
   data: () => ({
      type: "error",
      message: "",
   }),
   created() {
      EventBus.$on(Constants.EVENT_ERROR, (data) => {
         this.type = Constants.EVENT_ERROR;
         this.message = data;
      });

      EventBus.$on(Constants.EVENT_SUCCESS, (data) => {
         this.type = Constants.EVENT_SUCCESS;
         this.message = data;

         // Auto clear success message after 3 seconds
         setTimeout(() => {
            if (this.type === Constants.EVENT_SUCCESS) {
               this.message = "";
            }
         }, 3000);
      });
   },
   watch: {
      $route() {
         this.message = "";
      },
   },
};
</script>

<style scoped>
p {
   padding: 6pt;
   border-radius: 5px;
}

p.error {
   border: 1px solid rgb(214, 51, 51);
   background-color: rgb(255, 208, 208);
   color: #7a1a1a;
}

p.success {
   border: 1px solid rgb(0, 110, 0);
   background-color: rgb(181, 252, 181);
   color: #14521a;
}
</style>
