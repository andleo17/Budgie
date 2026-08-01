import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';
import LanguageDetector from 'i18next-browser-languagedetector';
import { z } from 'zod';
import en from './locales/en';
import es from './locales/es';

i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources: {
      en: { translation: en },
      es: { translation: es },
    },
    fallbackLng: 'en',
    load: 'languageOnly',
    interpolation: {
      escapeValue: false,
    },
  });

z.setErrorMap((issue) => {
  switch (issue.code) {
    case 'too_small': {
      if (issue.minimum === 1) return { message: i18n.t('validation.required') };
      return { message: i18n.t('validation.minLength', { count: String(issue.minimum) }) };
    }
    case 'too_big': {
      return { message: i18n.t('validation.maxLength', { count: String(issue.maximum) }) };
    }
    case 'invalid_format': {
      if (issue.format === 'email') return { message: i18n.t('validation.email') };
      if (issue.format === 'regex') return { message: i18n.t('validation.invalidFormat') };
      return { message: i18n.t('validation.invalid') };
    }
    case 'invalid_type': {
      if (issue.received === 'undefined' || issue.received === 'null') {
        return { message: i18n.t('validation.required') };
      }
      return { message: i18n.t('validation.invalid') };
    }
    default:
      return { message: i18n.t('validation.invalid') };
  }
});

const detectedLocale = typeof navigator !== 'undefined'
  ? navigator.language
  : i18n.language || 'en';

export function getFormatLocale(): string {
  return detectedLocale;
}

export default i18n;
