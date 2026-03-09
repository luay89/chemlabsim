# 📜 دستور SaaS الهندسي

> **هذا المستودع هو "دستور هندسي" لبناء مشاريع SaaS صحيحة، آمنة، وقابلة للصمود.**

---

## 🔥 لماذا هذا الدستور؟

### المشكلة:

```
❌ بدأنا نبني features قبل أن نبني المناعة
❌ قرارات معمارية في رأس أحدهم لا في وثيقة
❌ أمان "يُتذكر" لا يُفرض
❌ كل مطور جديد يبدأ من الصفر
❌ وكلاء AI ينفلتون ويكسرون البنية
```

### الحل:

```
✅ دستور هندسي مكتوب — كل قاعدة موثّقة
✅ أمان يُفرض بالكود، لا بالتذكير
✅ بنية معمارية واضحة لا تُخترق
✅ مطور جديد يقرأ الدستور ويبدأ فوراً
✅ AI يعمل ضمن حدود محددة وقابلة للرقابة
```

---

## 🏛️ مخطط البنية (Architecture Layers)

```
┌─────────────────────────────────────────────────────────────┐
│  ① Philosophy Layer  —  المبادئ العليا التي تحكم كل شيء     │
├─────────────────────────────────────────────────────────────┤
│  ② Governance Layer  —  التحكم بالتغيير والحوكمة             │
├─────────────────────────────────────────────────────────────┤
│  ③ Architecture Layer  —  البنية المعمارية والطبقات          │
├─────────────────────────────────────────────────────────────┤
│  ④ Security Infrastructure  —  البنية الأمنية (10 قوانين)   │
├─────────────────────────────────────────────────────────────┤
│  ⑤ Domain Architecture  —  النمط المعماري الذهبي (withApi)  │
├─────────────────────────────────────────────────────────────┤
│  ⑥ API Standards  —  معايير الـ API الموحدة                  │
├─────────────────────────────────────────────────────────────┤
│  ⑦ Data Layer  —  عقيدة قاعدة البيانات                       │
├─────────────────────────────────────────────────────────────┤
│  ⑧ Testing & Quality  —  استراتيجية الاختبارات              │
├─────────────────────────────────────────────────────────────┤
│  ⑨ Observability  —  المراقبة والتتبع والتنبيهات            │
├─────────────────────────────────────────────────────────────┤
│  ⑩ Evolution Controls  —  التحكم بالتوسع ومنع الفوضى        │
└─────────────────────────────────────────────────────────────┘
```

---

## 📖 فهرس الملفات

| # | الملف | الوصف |
|---|-------|-------|
| 00 | [SAAS_OPERATING_SYSTEM_v1.0.md](./engineering/00_SAAS_OPERATING_SYSTEM_v1.0.md) | نظرة عامة على النظام الكامل — ابدأ من هنا |
| 01 | [PHILOSOPHY.md](./engineering/01_PHILOSOPHY.md) | الفلسفة التأسيسية — المبادئ العليا التي لا تُناقش |
| 02 | [GOVERNANCE.md](./engineering/02_GOVERNANCE.md) | نظام الحوكمة — طلبات التغيير، Tier System، Scope Guard |
| 03 | [ARCHITECTURE.md](./engineering/03_ARCHITECTURE.md) | البنية المعمارية — الطبقات، المجلدات، قواعد الاعتمادية |
| 04 | [ARCHITECTURE_CANON.md](./engineering/04_ARCHITECTURE_CANON.md) | النمط المعماري الذهبي — withApi، Import Matrix، Module Structure |
| 05 | [SECURITY_TEN_LAWS.md](./engineering/05_SECURITY_TEN_LAWS.md) | القوانين العشرة للأمان — لا SQLi، لا RCE، لا inline roles |
| 06 | [SECURITY_LAYERS.md](./engineering/06_SECURITY_LAYERS.md) | طبقات الأمان التفصيلية — البوابات السبع + الطبقات السبع |
| 07 | [FEATURE_SECURITY_CHECKLIST.md](./engineering/07_FEATURE_SECURITY_CHECKLIST.md) | قائمة التحقق الأمني لكل Feature — 7 أسئلة إلزامية |
| 08 | [API_STANDARDS.md](./engineering/08_API_STANDARDS.md) | معايير الـ API — RESTful naming، Response format، Validation |
| 09 | [DATABASE_DOCTRINE.md](./engineering/09_DATABASE_DOCTRINE.md) | عقيدة قاعدة البيانات — Migrations، Multi-tenancy، تصنيف الجداول |
| 10 | [TESTING_STRATEGY.md](./engineering/10_TESTING_STRATEGY.md) | استراتيجية الاختبارات — الهرم، Safety Gates، Architecture Tests |
| 11 | [DEPLOYMENT_DISCIPLINE.md](./engineering/11_DEPLOYMENT_DISCIPLINE.md) | انضباط النشر — Freeze Rules، CI Gate |
| 12 | [OBSERVABILITY.md](./engineering/12_OBSERVABILITY.md) | طبقة المراقبة — Logging، Alerting، Audit Trail |
| 13 | [EVOLUTION_ENTROPY_CONTROL.md](./engineering/13_EVOLUTION_ENTROPY_CONTROL.md) | التوسع والتحكم بالفوضى — Phase Guards، Domain Boundaries |
| 14 | [PERFORMANCE_STANDARDS.md](./engineering/14_PERFORMANCE_STANDARDS.md) | معايير الأداء — SLOs، Query Performance، Caching |
| 15 | [AI_INTERACTION_PROTOCOL.md](./engineering/15_AI_INTERACTION_PROTOCOL.md) | بروتوكول التعامل مع AI — ما يُسمح وما يُمنع |
| 16 | [AI_AGENT_GUIDELINES.md](./engineering/16_AI_AGENT_GUIDELINES.md) | إرشادات وكلاء AI — Prompts، Red Flags |
| 17 | [SYSTEMS_ENGINEER_DECISIONS.md](./engineering/17_SYSTEMS_ENGINEER_DECISIONS.md) | القرارات الذكية العشرة — PostgreSQL، Queues، ADR |
| 18 | [CODE_EXAMPLES.md](./engineering/18_CODE_EXAMPLES.md) | أمثلة الكود التطبيقية — withApi، CSRF، Zod |
| 19 | [GOLDEN_RULES_SUMMARY.md](./engineering/19_GOLDEN_RULES_SUMMARY.md) | القواعد الذهبية المختصرة — 17 قاعدة للطباعة والتعليق |
| 20 | [NEW_PROJECT_KICKSTART.md](./engineering/20_NEW_PROJECT_KICKSTART.md) | طريقة البدء الصحيحة في مشروع جديد — 3 أيام |
| 21 | [VERSIONING.md](./engineering/21_VERSIONING.md) | نظام إصدار النسخ — Semantic Versioning، Changelog |
| 22 | [AMENDMENT_PROTOCOL.md](./engineering/22_AMENDMENT_PROTOCOL.md) | بروتوكول التعديل — كيف يُعدَّل هذا الدستور |

### 📁 ملفات إضافية

| الملف | الوصف |
|-------|-------|
| [CHANGELOG.md](./CHANGELOG.md) | سجل التغييرات لكل إصدار |
| [docs/decisions/](./docs/decisions/) | قرارات البنية المعمارية (ADRs) |

---

## 🚀 Quick Start Guide — كيف يستخدم مطور جديد هذا الدستور؟

### اليوم الأول (30 دقيقة):

```
1. اقرأ 01_PHILOSOPHY.md          ← فهم المبادئ العليا
2. اقرأ 02_GOVERNANCE.md          ← كيف تطلب تغييراً
3. اقرأ 04_ARCHITECTURE_CANON.md  ← النمط الإلزامي (withApi)
4. اقرأ 19_GOLDEN_RULES_SUMMARY.md ← 17 قاعدة ذهبية
```

### عند بدء مشروع جديد:

```
1. اقرأ 20_NEW_PROJECT_KICKSTART.md ← الخطوات الثلاث
2. اقرأ 03_ARCHITECTURE.md          ← هيكل المجلدات
3. اقرأ 06_SECURITY_LAYERS.md       ← بوابات الأمان الإلزامية
4. اقرأ 09_DATABASE_DOCTRINE.md     ← قواعد قاعدة البيانات
```

### عند كتابة API جديد:

```
1. اقرأ 08_API_STANDARDS.md         ← معايير التسمية والاستجابة
2. راجع 18_CODE_EXAMPLES.md         ← مثال withApi الكامل
3. راجع 07_FEATURE_SECURITY_CHECKLIST.md ← 7 أسئلة إلزامية
```

### عند التعامل مع AI (Copilot، Cursor، إلخ):

```
1. اقرأ 16_AI_AGENT_GUIDELINES.md   ← القيود والـ Prompts
2. راجع 15_AI_INTERACTION_PROTOCOL.md ← ما يُسمح وما يُمنع
```

---

## ⚖️ المبادئ الأساسية (ملخص سريع)

```
╔══════════════════════════════════════════════════════════════╗
║  1. الأمان يُفرض، لا يُتذكر                                 ║
║  2. لا ميزة قبل المناعة                                      ║
║  3. كل قرار معماري يصمد 3 سنوات أو لا يُنفَّذ               ║
║  4. النظام يحمي المهندس من نفسه                              ║
║  5. AI مُنفّذ، لا مُصمّم                                     ║
║  6. لا كود بدون طلب تغيير مكتوب                              ║
║  7. كل API يستخدم withApi (لا exceptions)                    ║
║  8. كل Query يحتوي tenant_id (Tenant Isolation)              ║
║  9. لا مكتبات جديدة بدون إذن صريح                            ║
║  10. أي شك = توقف واسأل                                      ║
╚══════════════════════════════════════════════════════════════╝
```

---

## 📊 نسخة الدستور

| المعلومة | القيمة |
|---------|--------|
| **النسخة** | v1.2.2 |
| **الحالة** | نشط |
| **آخر تحديث** | 2026-02-20 |
| **عدد الملفات** | 23 ملف (00-22) |
| **التطبيق** | جميع مشاريع SaaS الحالية والمستقبلية |
| **قاعدة التعديل** | أي تغيير يتطلب ADR — راجع `22_AMENDMENT_PROTOCOL.md` |

---

> **"إذا كان النظام يحتاج إلى تذكير لكي يكون آمناً، فهو غير آمن."**
