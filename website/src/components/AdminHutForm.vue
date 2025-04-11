<template>
   <form @submit.prevent="handleSubmit">
      <div class="mb-3">
         <label for="name" class="form-label">{{ $t('message.name') }} *</label>
         <input type="text" class="form-control" id="name" v-model="form.name" required>
      </div>

      <div class="mb-3">
         <label for="country" class="form-label">{{ $t('message.country') }} *</label>
         <input type="text" class="form-control" id="country" v-model="form.country" required>
      </div>

      <div class="mb-3">
         <label for="region" class="form-label">{{ $t('message.region') }}</label>
         <input type="text" class="form-control" id="region" v-model="form.region">
      </div>

      <div class="mb-3">
         <label for="hutWebsite" class="form-label">{{ $t('message.website') }}</label>
         <input type="url" class="form-control" id="hutWebsite" v-model="form.hutWebsite">
      </div>

      <div class="mb-3">
         <label for="link" class="form-label">{{ $t('message.onlineBookingLink') }}</label>
         <input type="url" class="form-control" id="link" v-model="form.link">
      </div>

      <div class="row">
         <div class="col">
            <div class="mb-3">
               <label for="latitude" class="form-label">{{ $t('message.latitude') }}</label>
               <input type="number" step="0.000001" class="form-control" id="latitude" v-model="form.latitude">
            </div>
         </div>
         <div class="col">
            <div class="mb-3">
               <label for="longitude" class="form-label">{{ $t('message.longitude') }}</label>
               <input type="number" step="0.000001" class="form-control" id="longitude" v-model="form.longitude">
            </div>
         </div>
         <div class="col">
            <div class="mb-3">
               <label for="altitude" class="form-label">{{ $t('message.altitude') }}</label>
               <input type="number" class="form-control" id="altitude" v-model="form.altitude">
            </div>
         </div>
      </div>

      <div class="mb-3">
         <div class="form-check">
            <input type="checkbox" class="form-check-input" id="enabled" v-model="form.enabled">
            <label class="form-check-label" for="enabled">{{ $t('message.enabled') }}</label>
         </div>
      </div>

      <div class="modal-footer">
         <button type="button" class="btn btn-secondary" @click="$emit('cancel')">
            {{ $t('message.cancel') }}
         </button>
         <button type="submit" class="btn btn-primary">
            {{ $t('message.save') }}
         </button>
      </div>
   </form>
</template>

<script>
export default {
   props: {
      hut: {
         type: Object,
         required: true
      }
   },
   data() {
      return {
         form: this.initForm()
      }
   },
   methods: {
      initForm() {
         return {
            id: this.hut.id,
            name: this.hut.name,
            country: this.hut.country,
            region: this.hut.region,
            hutWebsite: this.hut.hutWebsite,
            link: this.hut.link,
            latitude: this.hut.latitude,
            longitude: this.hut.longitude,
            altitude: this.hut.altitude,
            enabled: this.hut.enabled,
            lastUpdated: this.hut.lastUpdated,
            added: this.hut.added,
            activated: this.hut.activated,
            manuallyEdited: this.hut.manuallyEdited
         }
      },
      handleSubmit() {
         this.$emit('save', { ...this.form });
      }
   },
   watch: {
      hut: {
         handler(newHut) {
            this.form = this.initForm();
         },
         deep: true
      }
   }
}
</script>

<style scoped>
.form-label {
   font-weight: 500;
}

.modal-footer {
   padding: 1rem 0 0 0;
   border-top: 1px solid #dee2e6;
}
</style>
