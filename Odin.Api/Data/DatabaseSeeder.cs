using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;

namespace Odin.Api.Data;

public class DatabaseSeeder(ApplicationDbContext context)
{
    public async Task SeedAsync()
    {
        await SeedReferenceCatalogAsync();
    }

    /// <summary>Ethnicities, eras, geo backfill, commerce catalog — safe to re-run when tables are empty (e.g. integration tests after Respawn).</summary>
    public async Task SeedReferenceCatalogAsync()
    {
        await SeedEthnicitiesAndRegionsAsync();
        await SeedErasAndPopulationsAsync();
        await SeedCatalogCommerceAsync();
    }

    public async Task SeedCatalogCommerceAsync()
    {
        if (await context.CatalogProducts.AnyAsync())
            return;

        var product = new CatalogProduct
        {
            ServiceType = OrderService.qpAdm,
            DisplayName = "qpAdm ancestry analysis",
            Description = "Deep ancestry modeling with reference populations.",
            BasePrice = 49.99m,
            IsActive = true
        };
        context.CatalogProducts.Add(product);
        await context.SaveChangesAsync();

        var addons = new[]
        {
            new ProductAddon
            {
                Code = "EXPEDITED",
                DisplayName = "Compute faster your results",
                Price = 20m,
                IsActive = true
            },
            new ProductAddon
            {
                Code = "Y_HAPLOGROUP",
                DisplayName = "Find your Y haplogroup",
                Price = 20m,
                IsActive = true
            },
            new ProductAddon
            {
                Code = "MERGE_RAW",
                DisplayName = "Merge your raw data",
                Price = 40m,
                IsActive = true
            }
        };
        context.ProductAddons.AddRange(addons);
        await context.SaveChangesAsync();

        foreach (var addon in addons)
        {
            context.CatalogProductAddons.Add(new CatalogProductAddon
            {
                CatalogProductId = product.Id,
                ProductAddonId = addon.Id
            });
        }

        context.PromoCodes.Add(new PromoCode
        {
            Code = "WELCOME10",
            DiscountType = PromoDiscountType.Percent,
            Value = 10m,
            IsActive = true,
            ApplicableService = OrderService.qpAdm,
            RedemptionCount = 0
        });

        await context.SaveChangesAsync();
    }

    private async Task SeedEthnicitiesAndRegionsAsync()
    {
        if (await context.Ethnicities.AnyAsync())
            return;

        var seedData = new Dictionary<string, string[]>
        {
            ["Greek"] =
            [
                "Peloponnese", "Attica", "Islander Greek", "Macedonia", "Thrace",
                "Epirus", "Thessaly", "Central Greece", "Magna Graecia",
                "Anatolian Greek", "Pontic Greek", "North Epirote Greek",
            ],
            ["Albanian"] =
            [
                "South Albanian", "North Albanian", "Kosovo Albanian", "Sanjak Albanian",
                "Serbia Albanian", "Macedonia Albanian", "Arvanite",
                "Greek Epirus Albanian", "Arbereshe",
            ],
            ["Serbian"] =
            [
                "Serbia Serbian", "RS Serbian (Republika Srpska Bosnia)",
                "Montenegro", "Kosovo",
            ],
            ["Turkish"] = ["Anatolian Turk", "Balkan Turk"],
            ["Bulgarian"] =
            [
                "East Bulgaria", "West Bulgaria", "North Macedonia Bulgarian",
                "Greek Thrace", "Turkey Bulgarian",
            ],
            ["Italian"] =
            [
                "North Italian", "Central Italian", "South Italian",
                "Sicilian", "Sardinian", "Corsica",
            ],
            ["North Macedonian"] = ["East Macedonia", "West Macedonia", "South Macedonia"],
            ["Pomak"] = [],
        };

        foreach (var (ethnicityName, regionNames) in seedData)
        {
            var ethnicity = new Ethnicity { Name = ethnicityName };
            context.Ethnicities.Add(ethnicity);

            foreach (var regionName in regionNames)
            {
                context.Regions.Add(new Region { Name = regionName, Ethnicity = ethnicity });
            }
        }

        await context.SaveChangesAsync();
    }

    private async Task SeedErasAndPopulationsAsync()
    {
        if (await context.Eras.AnyAsync())
            return;

        var now = DateTime.UtcNow;
        const string seeder = "DatabaseSeeder";

        var geoJsonMap = LoadPopulationGeoJson();

        var eras = new[]
        {
            new
            {
                Name = "Hunter Gatherer and Neolithic Farmer",
                Description =
                    "Encompasses the major ancestral components from the Mesolithic through the Neolithic transition (~12,000\u20134,000 BCE) identified through ancient DNA as the foundational genetic building blocks of modern Eurasian and global populations.",
                Populations = new (string Name, string Description, string IconFileName, string VideoFileName)[]
                {
                    ("Anatolian Neolithic Farmer",
                        "Represents the early farming populations of central and western Anatolia (c. 8500\u20136000 BCE) who showed approximately 80\u201390% genetic continuity with local pre-farming hunter-gatherers. These farmers carried deep Basal Eurasian ancestry, a lineage that diverged from other non-Africans before Neanderthal admixture occurred. Genomic evidence establishes Anatolia as the primary source of the European Neolithic gene pool, with these farmers spreading agriculture westward through the Aegean into mainland Europe. Multiple genetically differentiated farming populations coexisted across southwestern Asia, with the Anatolian lineage becoming foundational for subsequent Neolithic expansions. Their genetic legacy persists as a major ancestry component in modern European and West Asian populations.",
                        "anatolian-neolithic-farmer.svg", ""),
                    ("Western Steppe Herder",
                        "Descends from a merger of Eastern Hunter-Gatherers (EHG) and Caucasus Hunter-Gatherers (CHG) on the Pontic-Caspian steppe during the Chalcolithic period (~5th millennium BCE). This component is closely associated with the Yamnaya culture, whose massive expansion around 3000 BCE transformed the genetic and cultural landscape of Eurasia. Yamnaya-related migrations are linked to the dispersal of Indo-European languages across Europe, Central Asia, and South Asia. Individuals predominantly carried Y-chromosome haplogroup R1b-Z2103, and their genetic profile appears in both Corded Ware and Bell Beaker cultures. Western Steppe Herder ancestry remains a substantial component in modern European, Central Asian, and South Asian populations.",
                        "western-steppe-herder.svg", ""),
                    ("Western Hunter Gatherer",
                        "Represents the Mesolithic forager populations of western, southern, and central Europe (c. 15,000\u20135,000 BCE) who recolonized the continent after the Last Glacial Maximum. Typified by specimens such as Loschbour from Luxembourg, WHGs were identified by Lazaridis et al. (2014) as one of three deeply differentiated ancestral populations contributing to modern Europeans. Their ancestry traces to the Villabruna cluster, likely originating in the Balkans and associated with Epigravettian and Azilian cultures. WHGs were largely replaced by successive expansions of Anatolian-derived Early European Farmers during the Neolithic, though residual WHG ancestry persists across Europe. The highest proportions of surviving WHG ancestry are found today in the eastern Baltic region.",
                        "western-hunter-gatherer.svg", ""),
                    ("Caucasian Hunter Gatherer",
                        "A deeply divergent lineage of anatomically modern humans first identified from Upper Paleolithic and Mesolithic specimens in the southern Caucasus, including Satsurblia (~13,300 BP) and Kotias (~9,700 BP) from western Georgia. CHGs split from Western Hunter-Gatherers approximately 45,000 years ago, shortly after the initial expansion of modern humans into Europe. Their ancestry includes a significant Basal Eurasian component (~38\u201348%), with the remainder closer to Ancient North Eurasians. CHGs were a major contributor to the formation of the Yamnaya steppe herders who later migrated across Eurasia around 3,000 BCE. Modern populations in the Caucasus, Central Asia, and South Asia carry the highest proportions of CHG-related ancestry.",
                        "caucasian-hunter-gatherer.svg", ""),
                    ("Iranian Neolithic Farmer",
                        "Represents the early farming populations of the Zagros Mountains (~10,000 BCE), exemplified by individuals from Ganj Dareh who show evidence of early goat domestication. Though genetically closest to Caucasus Hunter-Gatherers, Zagros farmers were deeply divergent from contemporary Anatolian Neolithic Farmers, having separated approximately 46,000\u201377,000 years ago. They made little direct contribution to European populations, suggesting relative geographic isolation from other Fertile Crescent groups. Their genetic legacy instead flowed eastward, with modern Pakistani, Afghan, and Iranian Zoroastrian populations showing the closest affinities. The genetic data support a model of multiple, independent transitions to agriculture by distinct populations across southwestern Asia.",
                        "iranian-neolithic-farmer.svg", ""),
                    ("Natufian",
                        "Represents the Epipaleolithic hunter-gatherer culture of the Levant (~15,000\u201311,500 cal BP), considered among the earliest sedentary communities and precursors to Neolithic agriculture. Ancient DNA reveals that Natufians derived roughly half their ancestry from a Basal Eurasian lineage with minimal Neanderthal admixture, indicating deep divergence from other non-African populations. Genetic continuity between Natufian and early Levantine farming populations suggests that agriculture in the southern Levant arose from local foragers rather than incoming migrants. By the Bronze Age, admixture from Iranian, Anatolian, and European hunter-gatherer sources had substantially reshaped Levantine ancestry. Natufian-related genetic signatures persist as a foundational component in modern Near Eastern and parts of North African populations.",
                        "natufian.svg", ""),
                    ("North African Farmer",
                        "Encompasses the prehistoric agricultural populations of the Maghreb whose transition to farming (~7,400 years ago) involved multiple migration waves from Iberia and the Levant mixing with indigenous Iberomaurusian-descended foragers. The earliest Neolithic contexts show predominantly European Neolithic ancestry, followed by a Middle Neolithic influx of Levantine pastoralist ancestry. Indigenous North African lineages, traceable through Iberomaurusian specimens at Taforalt (~23,000 BP) and Afalou (~15,000 BP), demonstrate at least 20,000 years of genetic continuity in the region. By the Late Neolithic, European, Levantine, and local ancestries had blended into a distinctive North African genetic profile. This composite ancestry forms a key baseline for understanding subsequent Phoenician, Roman, and Arab-era demographic changes across the Maghreb.",
                        "north-african-farmer.svg", ""),
                    ("Northeast Asian",
                        "Refers to the deeply divergent East Asian-related ancestral lineage documented in Paleolithic and Mesolithic Siberian populations, carrying a mixture of paleo-Siberian and Ancient North Eurasian ancestry. Middle Holocene genomes from the Altai and Trans-Baikal reveal highly connected gene pools across North Asia, with ancient Northeast Asian ancestry linking Siberian, Central Asian, and Beringian populations. Prolonged gene flow between Northeastern Siberians and Native American-related groups highlights the role of this region as a crossroads between East Asian and Beringian demographics. This ancestry component is a key source for later Uralic-speaking and Turkic-speaking populations that expanded across northern Eurasia. In admixture modeling, Northeast Asian ancestry serves as a critical reference for reconstructing the population history of Inner and Northern Asia.",
                        "northeast-asian-neolithic.svg", ""),
                    ("Native American",
                        "Descends from populations that migrated from northeast Asia into the Americas via the Beringia land bridge during the late Pleistocene. Ancient DNA from the ~11,500-year-old Upward Sun River infants in Alaska revealed the first founding population of Native Americans, distinct from all known Asian groups. Most Indigenous Americans trace their ancestry to a single founding population (First Americans), though at least three separate streams of Asian gene flow contributed to the genetic diversity of the Americas. The initial peopling followed a rapid southward coastal expansion with sequential population splits and limited subsequent gene flow, particularly in South America. All ancient individuals in the Americas, except later-arriving Arctic peoples, are more closely related to contemporary Indigenous Americans than to any population elsewhere.",
                        "native-american.svg", ""),
                    ("Ancestral South Indian",
                        "A reconstructed ancestral population inferred from genetic modeling as one of the two primary sources of South Asian ancestry, deeply divergent from both Ancestral North Indians (ANI) and East Asians. ASI-related ancestry is maximized in tribal Dravidian-speaking groups of southern India and emerged from a mixture of Iranian farmer-related ancestry and ancient local hunter-gatherer lineages. Following the decline of the Indus Valley Civilization (~1900 BCE), southeastern populations mixed with Iranian-related groups to form the ASI component observed in modern South Asians. Recent archaeogenomic studies have identified a related Proto-Dravidian ancestry in tribal groups like the Koraga, dating to approximately 4,400 years ago. The ASI component is central to understanding the deep population structure of the Indian subcontinent.",
                        "ancient-ancestral-south-indian.svg", ""),
                    ("Sub Saharan Africans",
                        "Represents the deeply divergent ancestral lineages of sub-Saharan Africa, home to the greatest genetic diversity of any global region and the deepest branches of the human phylogenetic tree. Ancient DNA reveals that African forager populations maintained deeply structured ancestry dating to 80,000\u201320,000 years ago, with at least three highly divergent source populations contributing to modern sub-Saharan genetic variation. Southern African lineages ancestral to the San people represent one of the deepest-branching human lineages, historically spread across a much wider geographic range than today. Despite this deep structure, long-term genetic continuity within regions persisted for millennia, with limited long-range gene flow until Bantu expansions began approximately 3,000\u20135,000 years ago. Sub-Saharan ancestry serves as the outgroup reference in most Eurasian admixture models.",
                        "sub-saharan-african.svg", ""),
                    ("Baltic",
                        "Represents the Mesolithic and Neolithic forager populations of the eastern Baltic region, positioned at the genetic boundary between Western Hunter-Gatherers (WHG) and Eastern Hunter-Gatherers (EHG). Baltic hunter-gatherers carried mixed WHG and EHG ancestry, with additional Ancient North Eurasian (ANE) admixture from the east. The eastern Baltic was one of the last regions in Europe to adopt farming, with Mesolithic forager ancestry persisting well into the Middle Neolithic without gene flow from Central European farmers. Later Siberian-related ancestry arrived in the Iron Age, linking Baltic populations to Uralic speakers further east. The region's position as a genetic contact zone makes Baltic ancestry an important reference component in admixture models of Northern and Eastern Europe.",
                        "", ""),
                    ("Finno-Ugric",
                        "Refers to the ancestral populations associated with the dispersal of Uralic languages, characterized by a distinctive blend of European and Siberian genetic ancestry. Ancient DNA shows that Siberian migrations into northern Europe began at least 3,500 years ago, admixing into populations that would become speakers of modern Finnic and Sami languages. This Siberian ancestry arrived at the eastern Baltic coast no later than the mid-first millennium BCE, coinciding with the diversification of west Uralic and Finnic languages. Bronze Age and Iron Age transitions in the eastern Baltic brought successive waves of hunter-gatherer and Siberian genetic input that distinguish Finno-Ugric speakers from other European populations. Modern Finns carry distinctive East Asian-related genetic contributions that reflect these ancient Siberian migrations.",
                        "uralic.svg", ""),
                    ("Saami",
                        "The Sami are the indigenous Uralic-speaking people of northern Fennoscandia, with a genetic profile shaped by the convergence of ancient European and Siberian ancestries beginning at least 3,500 years ago. Their genome shows a predominantly European-derived base with approximately 6% East Asian ancestry, making them a genetic outlier among European populations. Mitochondrial DNA studies reveal both western (haplogroups V and U5b1b1) and eastern (haplogroup Z) maternal lineages, the latter indicating shared ancestry with Volga-Ural populations as recently as 2,700 years ago. Historically, Sami-related populations inhabited a much larger territory during the Iron Age than their present-day range in northern Scandinavia, Finland, and the Kola Peninsula. Their population isolate status and unique admixture history make them a valuable reference in studies of European genetic structure.",
                        "", ""),
                },
            },
            new
            {
                Name = "Classical Antiquity",
                Description =
                    "Represents the historically attested peoples and cultures from the Iron Age through the late Roman and early Medieval periods (~800 BCE\u2013700 CE) whose genetic profiles have been reconstructed through archaeogenomic sampling across the Mediterranean, Balkans, and wider Europe.",
                Populations = new (string Name, string Description, string IconFileName, string VideoFileName)[]
                {
                    ("Illyrian",
                        "An Indo-European-speaking confederation of tribes who inhabited the western Balkans during the Iron Age, occupying territory corresponding to modern Albania, Montenegro, Kosovo, Croatia, Bosnia, and western Serbia. Ancient DNA from Iron Age Balkan individuals reveals a genetic profile combining Neolithic farmer ancestry with Bronze Age steppe-derived contributions, reflecting Illyria's position at the crossroads of Mediterranean and continental European influences. Genomic analysis of over 6,000 ancient Balkan genomes demonstrates that modern Albanians descend in part from Roman-era western Balkan populations with genetic continuity traceable to Bronze Age Illyrians. Paternal lineages including J2b2a1-L283, E-V13, and R1b show pronounced continuity between ancient Illyrian-period and modern Albanian populations. Subsequent Roman imperial, Migration Period, and Slavic demographic changes significantly transformed the broader Balkan genetic landscape while Albanian populations retained substantial Illyrian-related ancestry.",
                        "illyrian.svg", ""),
                    ("Ancient Greek",
                        "Represents the Bronze Age and Classical populations of the Aegean and Greek mainland, whose genetic origins were illuminated by the landmark Lazaridis et al. (2017) study of Minoan and Mycenaean genomes. Both Minoans and Mycenaeans derived at least three-quarters of their ancestry from Neolithic farmers of western Anatolia, with an additional 9\u201317% from Caucasus/Iran-related sources. The key distinction was that Mycenaeans carried 4\u201316% steppe-related ancestry absent in Minoans, likely introduced during 3rd-millennium BCE migrations. These genetic inputs from both east and north may have served as cultural catalysts for the innovations associated with Mycenaean civilization. Modern Greeks closely resemble Mycenaeans but with additional dilution of Early Neolithic ancestry from subsequent admixture events.",
                        "ancient-greek.svg", "Ancient Greek.mp4"),
                    ("Thracian",
                        "An Indo-European-speaking people who inhabited the eastern Balkans and parts of western Anatolia from the Bronze Age through Roman times, occupying a geographic position between the Mediterranean and the Pontic steppe. Ancient mitochondrial DNA from Bronze Age Bulgaria reveals that Thracians held an intermediate genetic position between Early Neolithic farmers and Late Neolithic\u2013Bronze Age steppe pastoralists. Iron Age Thracian samples show continued genetic continuity with earlier European farmer populations, suggesting stable local ancestry in southeastern Europe over two millennia. The broader Balkan region where Thracians lived served as a nexus between eastern and western populations, experiencing intermittent steppe contact up to 2,000 years before the major northern European steppe migrations. Later Roman Imperial and Slavic Migration Period demographic changes largely replaced Thracian-specific ancestry in the region.",
                        "thracian.svg", ""),
                    ("Hittite & Phrygian",
                        "Represents the Anatolian Indo-European-speaking populations of the Bronze and Iron Ages, with the Hittites establishing one of the major Bronze Age empires of the Near East (~1650\u20131178 BCE). Ancient DNA shows that Proto-Hittite ancestry arrived in Anatolia from the Balkans via steppe-related migrations, blending approximately 10\u201320% steppe-derived ancestry with a dominant ~80% local northwest Anatolian Neolithic base. This pattern reflects a gradual admixture process during migration, where incoming Indo-European speakers progressively incorporated local populations. The Phrygians, who rose to prominence in central-western Anatolia after the Hittite collapse (~12th century BCE), share linguistic ties to the Graeco-Phrygian branch and likely carried a similar blend of Anatolian and steppe ancestry. Both populations exemplify how Indo-European language spread through elite cultural transmission rather than wholesale population replacement.",
                        "hittite-phrygian.svg", ""),
                    ("Phoenician",
                        "The seafaring Canaanite people of the Iron Age Levantine coast (~1500\u2013300 BCE) who established a vast trading network and colonial settlements across the Mediterranean, including Carthage, western Sicily, Sardinia, and Iberia. Archaeogenomic studies show that despite strong cultural, linguistic, and religious continuity between the Levantine homeland and western settlements, Levantine Phoenicians made surprisingly little genetic contribution to Punic colonial populations. Western Phoenician-associated communities instead derived most of their ancestry from local Mediterranean populations genetically similar to ancient Sicilians and Aegeans. Bronze Age Levantine populations, the ancestral pool of Phoenicians, carried a mixture of local Natufian-related, Anatolian farmer, and Iranian/Caucasus-related ancestries. Phoenician cultural expansion thus functioned more as a franchise model of cultural transmission than as large-scale colonization by Levantine settlers.",
                        "phoenician.svg", ""),
                    ("Celtic",
                        "The Iron Age peoples associated with the Hallstatt (~800\u2013450 BCE) and La Tene (~450\u201350 BCE) cultural horizons of central and western Europe, representing the earliest evidence of supra-regional organization north of the Alps. Ancient DNA from Hallstatt-period elite burials in southern Germany reveals dynastic succession among Celtic elites, with matrilineal kinship networks spanning up to 100 kilometers between burial mounds. The genetic ancestry of Hallstatt elites was broadly shared across a wide geographic range from Iberia through central-eastern Europe, indicating pan-European population connections. Isotopic evidence confirms trans-regional mobility among Celtic individuals, reflecting long-distance exchange networks. After the late Iron Age (~450 BCE\u201350 CE), Celtic-associated ancestry underwent significant decline across much of its former range, replaced by Germanic and Roman demographic influences.",
                        "insular-celt.svg", ""),
                    ("Iberian",
                        "Refers to the pre-Roman populations of the Iberian Peninsula whose genetic history was shaped by dramatic Bronze Age transformations. Olalde et al. (2019) demonstrated that by ~2000 BCE, approximately 40% of Iberia's total ancestry and nearly 100% of its Y-chromosomes had been replaced by populations carrying steppe-derived ancestry, indicating a heavily male-dominated migration. By the Iron Age, steppe ancestry had spread throughout the peninsula, penetrating both Indo-European and non-Indo-European-speaking regions alike. Modern Basques represent a relatively unadmixed Iron Age Iberian population, having escaped the subsequent Roman and Moorish-period admixture events that reshaped the rest of the peninsula. The Iberian genetic profile in admixture models reflects this distinctive combination of Neolithic farmer, Western Hunter-Gatherer, and steppe ancestries.",
                        "iberian.svg", ""),
                    ("Punic Carthage",
                        "Represents the population of Carthage and associated Punic settlements across the central and western Mediterranean (~6th\u20132nd centuries BCE), recently revealed as one of antiquity's most genetically cosmopolitan civilizations. A 2025 study of 210 ancient genomes from 14 Punic sites shows that despite maintaining Phoenician cultural and religious traditions, Punic communities derived most of their ancestry from local Mediterranean populations resembling ancient Sicilians and Aegeans. Levantine Phoenician genetic contribution was negligible, representing the first known case of complete mismatch between cultural continuity and genetic ancestry. Secondary ancestry from North African populations grew as Carthage's political influence expanded, though it remained a minority component even within Carthage itself. Punic sites across the Mediterranean showed consistently high genetic diversity, reflecting a shared network of demographic processes spanning the Phoenician-Punic world.",
                        "punic-carthage.svg", ""),
                    ("Hellenistic Pontus",
                        "Represents the Greek colonial populations established along the Black Sea coast of Anatolia beginning around 700 BCE, including major settlements at Sinope, Trapezus (modern Trabzon), and Amisos. These colonies formed part of the broader Hellenistic cultural sphere that persisted through the Kingdom of Pontus (281\u201363 BCE) and into the Roman provincial period. Genomic studies of ancient Anatolian populations indicate remarkable genetic continuity from the Neolithic through the Roman and Byzantine periods, with local Anatolian populations forming the demographic core of the region. Pontic Greek communities likely carried a mixture of Aegean Greek ancestry and local Anatolian genetic contributions accumulated through centuries of settlement and intermarriage. The genetic legacy of Pontic Greeks persisted through the Byzantine era and is reflected in modern Pontic Greek diaspora communities.",
                        "hellenistic-pontus.svg", ""),
                    ("Latin and Etruscan",
                        "Represents the two dominant Iron Age populations of central Italy: the Latin-speaking founders of Rome and the linguistically distinct Etruscans of Tuscany and surrounding regions. Antonio et al. (2019) analyzed 127 ancient genomes from Rome and central Italy spanning 12,000 years, revealing that by Rome's founding (~8th century BCE), the region's genetic composition resembled modern Mediterranean populations. Despite speaking a non-Indo-European language, Iron Age Etruscans carried steppe-derived ancestry similar to their Italic neighbors, challenging models linking language to genetic origin. During the Roman Imperial period, central Italian populations received substantial Near Eastern gene flow, followed by increased European contributions reflecting Rome's geopolitical reach. A follow-up study of 82 Etruscan individuals confirmed a relatively stable local gene pool through the Iron Age, followed by an abrupt shift to ~50% eastern Mediterranean admixture during the Imperial period.",
                        "italic-and-etruscan.svg", ""),
                    ("Roman Moesia Superior",
                        "Represents the genetically cosmopolitan populations of the Roman frontier province in the central Balkans (modern Serbia), centered on the military site of Viminacium. Genomic analysis of over 136 ancient individuals from the 1st millennium CE reveals that despite extensive Roman military and cultural presence, there was surprisingly little Italic-derived ancestry in the provincial population. Instead, a large-scale influx of Anatolian-related ancestry occurred during the Imperial period, mirroring demographic shifts observed in Rome itself. Between approximately 250\u2013550 CE, the province received genetically diverse migrants from Central/Northern Europe and the steppe, confirming that Migration Period movements were ethnically heterogeneous confederations. The cosmopolitan frontier was ultimately transformed by Slavic migrations that contributed 30\u201360% of the ancestry of modern Balkan populations.",
                        "roman-moesia.svg", ""),
                    ("Medieval Albanian",
                        "Represents the early Medieval population of the western Balkans that bridges the Illyrian-Roman period and the emergence of the Albanian ethnic identity attested in historical sources from the 11th century CE. Ancient DNA analysis demonstrates substantial genetic continuity between Roman-era western Balkan populations and modern Albanians, with additional limited admixture from Slavic-related groups. Paternal Y-chromosome lineages including J2b2a1-L283 and E-V13 show pronounced continuity from Bronze Age Balkans through the Medieval period, supporting the Illyrian origin hypothesis. The Komani-Kruja archaeological culture (6th\u20139th centuries CE) shows no demographic rupture during the Slavic migrations, reflecting a Latin-Illyrian civilization that survived the broader upheavals of the Migration Period. While neighboring populations were substantially Slavicized, Medieval Albanians retained approximately 75\u201380% pre-Slavic Balkan ancestry.",
                        "proto-albanian.svg", ""),
                    ("Roman East Mediterranean",
                        "Encompasses the diverse populations of the eastern Roman provinces spanning the Levant, Egypt, and the Aegean during the Imperial period (1st century BCE\u20137th century CE). Ancient DNA from Imperial-period Rome shows that the city itself received massive immigration from the eastern Mediterranean, with Near Eastern ancestry becoming predominant during the height of the Empire. Egyptian populations of this era carried a mixture of North African Neolithic ancestry and eastern Fertile Crescent contributions, with sub-Saharan African ancestry increasing in post-Roman periods. The Roman eastern Mediterranean was a zone of intense genetic mixing, with Levantine populations carrying composite ancestry from Natufian, Anatolian farmer, and Iranian/Caucasus sources accumulated since the Bronze Age. This cosmopolitan genetic profile reflects the connectivity of Roman provincial administration and trade networks across the eastern basin.",
                        "roman-east-mediterranean.svg", ""),
                    ("Germanic",
                        "Refers to the populations associated with the dispersal of Germanic languages from a Scandinavian homeland, with steppe-derived ancestry entering Scandinavia from the Baltic region around 4,000 years ago. By approximately 1,650 years before present, a southward migration from southern Scandinavia into previously Celtic-speaking areas of present-day Germany, Poland, and the Netherlands is evident in the genomic record. During the Migration Period (~200\u2013550 CE), Germanic ancestry spread further through Anglo-Saxon settlement in Britain and Langobard expansion into Italy. A concurrent northward back-migration into Denmark and southern Sweden corresponds with the rise of Danish influence and the emergence of Old Norse. Integration of over 4,000 ancient genomes has revealed how Germanic language spread correlated with distinct genetic ancestry turnover across western Eurasia.",
                        "germanic.svg", ""),
                    ("Medieval Slavic",
                        "Represents the populations associated with the large-scale Slavic expansion across Eastern Europe and the Balkans during the 6th\u20138th centuries CE, one of the most significant demographic transformations in European history. Ancient DNA evidence demonstrates that Slavic-speaking migrants replaced more than 80% of the local gene pool in Eastern Germany, Poland, and Croatia, and contributed 30\u201360% of the ancestry of modern Balkan peoples. The genetic data show no evidence of sex-biased admixture, distinguishing Slavic expansion from earlier male-dominated steppe migrations. Regional variation in the degree of admixture with indigenous populations indicates that Slavic settlement involved both population replacement and cultural assimilation in different areas. Archaeological correlates include profound changes in material culture beginning in the 6th century, supporting an immigration model rather than cultural diffusion alone.",
                        "medieval-slav.svg", ""),
                    ("Roman North Africa",
                        "Encompasses the populations of North Africa during the period of Roman provincial administration (~146 BCE\u2013430s CE), a region inhabited by indigenous Berber (Amazigh) peoples shaped by millennia of local genetic continuity and successive Mediterranean contacts. Ancient DNA from the broader Maghreb shows that indigenous North African lineages trace back over 20,000 years to Iberomaurusian foragers, forming a deeply rooted genetic substrate. The Neolithic transition introduced European and Levantine farmer ancestry, and subsequent Phoenician colonization (from ~1200 BCE) added further Mediterranean diversity before Roman conquest. Under Roman rule, North Africa became one of the empire's most prosperous regions, with cosmopolitan cities like Carthage, Leptis Magna, and Hippo Regius facilitating continued demographic exchange across the Mediterranean. Later Vandal, Byzantine, and Arab demographic inputs further transformed the region's genetic landscape, though Berber-related ancestry remains prominent in modern Maghrebi populations.",
                        "berber.svg", ""),
                    ("Roman West Anatolia",
                        "Represents the populations of western Anatolia during the Roman provincial period, a region centered on major cities such as Ephesus, Pergamon, and Smyrna that had been shaped by millennia of Greek colonization and Hellenistic culture. Archaeogenomic studies reveal extraordinary genetic continuity in Anatolia from the Neolithic through the Roman and Byzantine periods, with local Anatolian populations serving as the demographic core of much of the eastern Roman Empire. Stable isotope and ancient DNA analyses from Roman Ephesus (2nd\u20133rd centuries CE) indicate a cosmopolitan population engaged in maritime trade, with evidence of migration into the city. The genetic profile of Roman western Anatolians combined a dominant local Anatolian Neolithic base with layers of Greek, Iranian/Caucasus, and broader eastern Mediterranean admixture accumulated over millennia. This region's demographic continuity and cosmopolitan character distinguish it from the more disrupted genetic histories of the western Mediterranean and Balkans during the same period.",
                        "", ""),
                },
            },
        };

        // Music track data: (displayOrder, name, fileName, populationNames[])
        var musicTrackData = new (int Order, string Name, string FileName, string[] Populations)[]
        {
            // Era 1 — Hunter Gatherer and Neolithic Farmer
            (1, "European Foragers", "european-foragers.wav", ["Western Hunter Gatherer", "Baltic", "Finno-Ugric", "Saami"]),
            (2, "Eurasian Steppe", "eurasian-steppe.wav", ["Western Steppe Herder"]),
            (3, "Near Eastern / Anatolian Farmers", "near-eastern-farmers.wav",
                ["Anatolian Neolithic Farmer", "Iranian Neolithic Farmer", "Caucasian Hunter Gatherer", "Ancestral South Indian"]),
            (4, "Levantine & North African", "levantine-north-african.wav", ["Natufian", "North African Farmer"]),
            (5, "East Asian & Native American", "east-asian-native-american.wav", ["Northeast Asian", "Native American"]),
            (6, "Sub-Saharan African", "sub-saharan-african.wav", ["Sub Saharan Africans"]),

            // Era 2 — Classical Antiquity
            (7, "Hellenic", "hellenic.wav",
                ["Ancient Greek", "Roman West Anatolia", "Hellenistic Pontus", "Roman East Mediterranean"]),
            (8, "Roman / Italic", "roman-italic.wav", ["Latin and Etruscan", "Roman Moesia Superior", "Roman North Africa"]),
            (9, "Balkan / Paleo-Balkan", "balkan-paleo-balkan.wav", ["Illyrian", "Thracian", "Medieval Albanian"]),
            (10, "Anatolian / Caucasian", "anatolian-caucasian.wav", ["Hittite & Phrygian"]),
            (11, "Semitic / Phoenician", "semitic-phoenician.wav", ["Phoenician", "Punic Carthage"]),
            (12, "Celtic", "celtic.wav", ["Celtic"]),
            (13, "Western Mediterranean", "western-mediterranean-pre-ie.wav", ["Iberian"]),
            (14, "Germanic", "germanic-sarmatian.wav", ["Germanic"]),
            (15, "Medieval Slavic", "medieval-slavic.wav", ["Medieval Slavic"]),
        };

        // Build reverse map: population name -> MusicTrack entity
        var popToTrack = new Dictionary<string, MusicTrack>();
        foreach (var (order, name, fileName, populationNames) in musicTrackData)
        {
            var track = new MusicTrack
            {
                Name = name,
                FileName = fileName,
                DisplayOrder = order,
                CreatedAt = now,
                CreatedBy = seeder,
                UpdatedAt = now,
            };
            context.MusicTracks.Add(track);

            foreach (var popName in populationNames)
                popToTrack[popName] = track;
        }

        foreach (var eraData in eras)
        {
            var era = new Era
            {
                Name = eraData.Name,
                Description = eraData.Description,
                CreatedAt = now,
                CreatedBy = seeder,
                UpdatedAt = now,
            };
            context.Eras.Add(era);

            foreach (var (popName, popDescription, iconFile, videoFile) in eraData.Populations)
            {
                context.Populations.Add(new Population
                {
                    Name = popName,
                    Description = popDescription,
                    Era = era,
                    GeoJson = geoJsonMap.GetValueOrDefault(popName, ""),
                    IconFileName = iconFile,
                    VideoFileName = videoFile,
                    MusicTrack = popToTrack[popName],
                    CreatedAt = now,
                    CreatedBy = seeder,
                    UpdatedAt = now,
                });
            }
        }

        await context.SaveChangesAsync();
    }

    private static Dictionary<string, string> LoadPopulationGeoJson()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "SeedData", "population-geojson.json");
        if (!File.Exists(path))
            return new Dictionary<string, string>();

        var json = File.ReadAllText(path);
        var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
        if (raw is null)
            return new Dictionary<string, string>();

        var result = new Dictionary<string, string>();
        foreach (var (name, featureCollection) in raw)
        {
            var geometry = ExtractGeometry(featureCollection);
            if (geometry is not null)
                result[name] = geometry;
        }
        return result;
    }

    /// <summary>
    /// Extracts a raw Polygon or MultiPolygon geometry JSON string from a
    /// GeoJSON FeatureCollection. If the collection contains a single Feature,
    /// the geometry is returned as-is. If it contains multiple Features with
    /// Polygon geometries, they are merged into a single MultiPolygon so the
    /// frontend can consume a uniform Polygon | MultiPolygon value.
    /// </summary>
    private static string? ExtractGeometry(JsonElement featureCollection)
    {
        if (featureCollection.ValueKind != JsonValueKind.Object)
            return null;

        if (!featureCollection.TryGetProperty("features", out var features)
            || features.ValueKind != JsonValueKind.Array
            || features.GetArrayLength() == 0)
            return null;

        if (features.GetArrayLength() == 1)
        {
            var singleFeature = features[0];
            if (singleFeature.TryGetProperty("geometry", out var geo))
                return geo.GetRawText();
            return null;
        }

        var polygonCoords = new List<string>();
        foreach (var feature in features.EnumerateArray())
        {
            if (!feature.TryGetProperty("geometry", out var geo))
                continue;

            if (!geo.TryGetProperty("type", out var geoType))
                continue;

            var typeStr = geoType.GetString();
            if (typeStr == "Polygon" && geo.TryGetProperty("coordinates", out var coords))
            {
                polygonCoords.Add(coords.GetRawText());
            }
            else if (typeStr == "MultiPolygon" && geo.TryGetProperty("coordinates", out var multiCoords))
            {
                foreach (var polyCoord in multiCoords.EnumerateArray())
                    polygonCoords.Add(polyCoord.GetRawText());
            }
        }

        if (polygonCoords.Count == 0)
            return null;

        if (polygonCoords.Count == 1)
            return $"{{\"type\":\"Polygon\",\"coordinates\":{polygonCoords[0]}}}";

        return $"{{\"type\":\"MultiPolygon\",\"coordinates\":[{string.Join(",", polygonCoords)}]}}";
    }

}
